using System.Text.RegularExpressions;
using WinAcmeGui.Application.Discovery;
using WinAcmeGui.Infrastructure.Configuration;

namespace WinAcmeGui.Infrastructure.Discovery;

public sealed class InstallationValidator(
    IWinAcmeVersionProbe versionProbe,
    Func<string, CancellationToken, Task<string>>? configurationPathResolver = null) : IInstallationValidator
{
    private static readonly Regex VersionPattern = new(@"(?<major>\d+)\.(?<minor>\d+)\.(?<build>\d+)(?:\.(?<revision>\d+))?", RegexOptions.Compiled);

    public async Task<InstallationCandidate?> ValidateAsync(string executablePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(executablePath)) return null;
        string versionText;
        try { versionText = await versionProbe.GetVersionAsync(executablePath, cancellationToken); }
        catch (Exception) { return null; }
        var match = VersionPattern.Match(versionText);
        if (!match.Success) return null;
        var configPath = configurationPathResolver is not null
            ? await configurationPathResolver(executablePath, cancellationToken)
            : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(executablePath))!, "settings.json");
        return new(executablePath, match.Value, configPath);
    }
}
