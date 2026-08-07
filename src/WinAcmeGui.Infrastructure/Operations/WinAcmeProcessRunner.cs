using System.Diagnostics;
using WinAcmeGui.Application.Operations;
using WinAcmeGui.Domain.Operations;

namespace WinAcmeGui.Infrastructure.Operations;

public sealed class WinAcmeProcessRunner(TimeSpan? timeout = null) : IWinAcmeRunner
{
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromMinutes(30);

    public async Task<OperationResult> RunAsync(WinAcmeCommand command, IProgress<string>? output, CancellationToken cancellationToken)
    {
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

        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start operation process.");
        var lines = new List<string>();
        var redactor = new OutputRedactor(command.Arguments.Where(x => x.IsSecret).Select(x => x.Value));
        async Task ReadAsync(StreamReader reader)
        {
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                var safeLine = redactor.Redact(line);
                lines.Add(safeLine);
                output?.Report(safeLine);
            }
        }

        var stdout = ReadAsync(process.StandardOutput);
        var stderr = ReadAsync(process.StandardError);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeout);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
            await Task.WhenAll(stdout, stderr);
            var status = process.ExitCode == 0 ? OperationStatus.Succeeded : OperationStatus.Failed;
            return new(status, process.ExitCode, Stopwatch.GetElapsedTime(start), lines, process.ExitCode == 0 ? null : "process.exit.nonzero");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || timeoutCts.IsCancellationRequested)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            var status = cancellationToken.IsCancellationRequested ? OperationStatus.Cancelled : OperationStatus.TimedOut;
            return new(status, null, Stopwatch.GetElapsedTime(start), lines, status == OperationStatus.Cancelled ? "operation.cancelled" : "operation.timeout");
        }
    }
}
