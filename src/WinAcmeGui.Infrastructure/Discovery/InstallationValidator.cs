using System.Text.RegularExpressions;
using WinAcmeGui.Application.Configuration;
using WinAcmeGui.Application.Discovery;
using WinAcmeGui.Domain.Installations;
using WinAcmeGui.Infrastructure.Configuration;
using WinAcmeGui.Infrastructure.Downloads;

namespace WinAcmeGui.Infrastructure.Discovery;

public sealed class InstallationValidator(
    IWinAcmeVersionProbe versionProbe,
    IWinAcmeConfigurationReader? configurationReader = null,
    Func<string, CancellationToken, Task<string>>? configurationPathResolver = null) : IInstallationValidator
{
    private static readonly Regex VersionPattern = new(@"(?<major>\d+)\.(?<minor>\d+)\.(?<build>\d+)(?:\.(?<revision>\d+))?", RegexOptions.Compiled);

    public async Task<InstallationCandidate?> ValidateAsync(string executablePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(executablePath)) return null;
        string versionText;
        try { versionText = await versionProbe.GetVersionAsync(executablePath, cancellationToken); }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return null; }
        var match = VersionPattern.Match(versionText.Trim());
        if (!match.Success) return null;
        var configPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(executablePath))!, "settings.json");
        AcmeEndpoint? endpoint = null;
        ConfigurationSnapshot? configurationSnapshot = null;
        if (configurationPathResolver is not null)
        {
            configPath = await configurationPathResolver(executablePath, cancellationToken);
        }
        else if (configurationReader is not null)
        {
            try
            {
                var configuration = await configurationReader.ReadAsync(executablePath, cancellationToken);
                configPath = configuration.ConfigurationPath;
                endpoint = configuration.Endpoint;
                configurationSnapshot = configuration;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { }
        }
        return new(executablePath, match.Value, configPath, endpoint, configurationSnapshot);
    }
}
