using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using WinAcmeGui.Application.Operations;
using WinAcmeGui.Domain.Operations;

namespace WinAcmeGui.Infrastructure.Operations;

public sealed class WinAcmeProcessRunner(TimeSpan? timeout = null) : IWinAcmeRunner
{
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromMinutes(30);

    public async Task<OperationResult> RunAsync(WinAcmeCommand command, IProgress<string>? output, CancellationToken cancellationToken)
    {
        if (!Path.IsPathFullyQualified(command.ExecutablePath))
            throw new ArgumentException("Executable path must be absolute.", nameof(command));

        var start = Stopwatch.GetTimestamp();
        var info = new ProcessStartInfo(command.ExecutablePath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(command.ExecutablePath) ?? Environment.CurrentDirectory,
            // wacs emits UTF-8; without this the redirected streams decode with the OEM code page
            // and non-ASCII domain names garble before redaction.
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in command.Arguments)
        {
            info.ArgumentList.Add(argument.Name);
            if (argument.Value.Length > 0) info.ArgumentList.Add(argument.Value);
        }

        Process process;
        try
        {
            process = Process.Start(info) ?? throw new InvalidOperationException("Could not start operation process.");
        }
        // Distinct codes keep the troubleshooting guide actionable: access denied and file-not-found
        // have completely different remedies.
        catch (Win32Exception ex) when (ex.NativeErrorCode == 2 || ex.NativeErrorCode == 3)
        {
            return new(OperationStatus.Failed, null, Stopwatch.GetElapsedTime(start), [], "process.start.notfound");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 5)
        {
            return new(OperationStatus.Failed, null, Stopwatch.GetElapsedTime(start), [], "process.start.denied");
        }
        catch (Win32Exception)
        {
            return new(OperationStatus.Failed, null, Stopwatch.GetElapsedTime(start), [], "process.start.failed");
        }
        catch (FileNotFoundException)
        {
            return new(OperationStatus.Failed, null, Stopwatch.GetElapsedTime(start), [], "process.start.notfound");
        }

        using (process)
        using (var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        using (var readerCts = CancellationTokenSource.CreateLinkedTokenSource(operationCts.Token))
        {
            operationCts.CancelAfter(_timeout);
            var lines = new ConcurrentQueue<string>();
            var redactor = new OutputRedactor(command.Arguments.Where(x => x.IsSecret).Select(x => x.Value));

            async Task ReadAsync(StreamReader reader)
            {
                while (await reader.ReadLineAsync(readerCts.Token) is { } line)
                {
                    var safeLine = redactor.Redact(line);
                    lines.Enqueue(safeLine);
                    output?.Report(safeLine);
                }
            }

            var stdout = ReadAsync(process.StandardOutput);
            var stderr = ReadAsync(process.StandardError);
            try
            {
                await process.WaitForExitAsync(operationCts.Token);
            }
            catch (OperationCanceledException) when (operationCts.IsCancellationRequested)
            {
                await TerminateAsync(process);
                await ObserveAsync(stdout);
                await ObserveAsync(stderr);
                var status = cancellationToken.IsCancellationRequested ? OperationStatus.Cancelled : OperationStatus.TimedOut;
                return new(status, null, Stopwatch.GetElapsedTime(start), lines.ToArray(), status == OperationStatus.Cancelled ? "operation.cancelled" : "operation.timeout");
            }

            // A child that inherited the redirected handles and outlives wacs keeps the pipes open;
            // bound the drain so an orphan cannot hold a finished run hostage until the full
            // operation timeout fires and misclassifies a successful exit.
            readerCts.CancelAfter(TimeSpan.FromSeconds(10));
            try
            {
                await Task.WhenAll(stdout, stderr);
            }
            catch (OperationCanceledException) when (!operationCts.IsCancellationRequested)
            {
                // Drain timed out with a healthy exit code: report what was captured.
            }
            var finalStatus = process.ExitCode == 0 ? OperationStatus.Succeeded : OperationStatus.Failed;
            return new(finalStatus, process.ExitCode, Stopwatch.GetElapsedTime(start), lines.ToArray(), process.ExitCode == 0 ? null : "process.exit.nonzero");
        }
    }

    private static async Task TerminateAsync(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }

        try
        {
            using var waitCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(waitCancellation.Token);
        }
        catch (OperationCanceledException) { }
        catch (InvalidOperationException) { }
    }

    private static async Task ObserveAsync(Task task)
    {
        try { await task; }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }
}
