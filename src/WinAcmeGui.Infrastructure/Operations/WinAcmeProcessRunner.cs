using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
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
            WorkingDirectory = Path.GetDirectoryName(command.ExecutablePath) ?? Environment.CurrentDirectory
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
        catch (Win32Exception)
        {
            return new(OperationStatus.Failed, null, Stopwatch.GetElapsedTime(start), [], "process.start.failed");
        }
        catch (FileNotFoundException)
        {
            return new(OperationStatus.Failed, null, Stopwatch.GetElapsedTime(start), [], "process.start.failed");
        }

        using (process)
        using (var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            operationCts.CancelAfter(_timeout);
            var lines = new ConcurrentQueue<string>();
            var redactor = new OutputRedactor(command.Arguments.Where(x => x.IsSecret).Select(x => x.Value));

            async Task ReadAsync(StreamReader reader)
            {
                while (await reader.ReadLineAsync(operationCts.Token) is { } line)
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
                await Task.WhenAll(stdout, stderr);
                var status = process.ExitCode == 0 ? OperationStatus.Succeeded : OperationStatus.Failed;
                return new(status, process.ExitCode, Stopwatch.GetElapsedTime(start), lines.ToArray(), process.ExitCode == 0 ? null : "process.exit.nonzero");
            }
            catch (OperationCanceledException) when (operationCts.IsCancellationRequested)
            {
                await TerminateAsync(process);
                await ObserveAsync(stdout);
                await ObserveAsync(stderr);
                var status = cancellationToken.IsCancellationRequested ? OperationStatus.Cancelled : OperationStatus.TimedOut;
                return new(status, null, Stopwatch.GetElapsedTime(start), lines.ToArray(), status == OperationStatus.Cancelled ? "operation.cancelled" : "operation.timeout");
            }
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
