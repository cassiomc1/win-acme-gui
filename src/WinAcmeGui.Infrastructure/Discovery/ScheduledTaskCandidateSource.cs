using System.Diagnostics;
using System.Text.RegularExpressions;
using WinAcmeGui.Application.Discovery;

namespace WinAcmeGui.Infrastructure.Discovery;

public static partial class ScheduledTaskCandidateParser
{
    [GeneratedRegex(@"^Task To Run:\s*(?:""(?<path>[^""]+\.exe)""|(?<path>[^\s]+\.exe))", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex TaskCommandRegex();

    public static IReadOnlyCollection<string> Parse(string output) =>
        TaskCommandRegex().Matches(output).Select(x => x.Groups["path"].Value).Where(x => x.Contains("wacs", StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}

public sealed class ScheduledTaskCandidateSource : IInstallationCandidateSource
{
    public async Task<IReadOnlyCollection<string>> FindAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) return [];
        var info = new ProcessStartInfo("schtasks.exe", "/query /fo LIST /v")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var process = Process.Start(info);
        if (process is null) return [];

        // Bound the whole probe so a wedged schtasks.exe can never stall discovery, and make sure
        // the process is killed (not just disposed) on cancellation or failure.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            // Drain stderr concurrently: schtasks writing past the pipe buffer without a reader deadlocks.
            var stderr = process.StandardError.ReadToEndAsync(timeoutCts.Token);
            var output = await process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            await stderr.ConfigureAwait(false);
            await process.WaitForExitAsync(timeoutCts.Token);
            return ScheduledTaskCandidateParser
                .Parse(output)
                .Select(path => Environment.ExpandEnvironmentVariables(path))
                .ToArray();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return [];
        }
        finally
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch (Exception)
            {
                // Already gone or access denied; nothing left to clean up.
            }
        }
    }
}
