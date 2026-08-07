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
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return ScheduledTaskCandidateParser.Parse(output);
    }
}
