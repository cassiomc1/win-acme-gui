using WinAcmeGui.Domain.Installations;

namespace WinAcmeGui.Application.Configuration;

public interface IWinAcmeConfigurationReader
{
    Task<ConfigurationSnapshot> ReadAsync(string executablePath, CancellationToken cancellationToken);
}

public sealed record ConfigurationSnapshot(
    string SettingsPath,
    string ClientName,
    string ConfigurationPath,
    AcmeEndpoint Endpoint,
    IReadOnlyDictionary<string, string> Values);
