using WinAcmeGui.Application.Configuration;
using WinAcmeGui.Application.Renewals;
using WinAcmeGui.Domain.Installations;
using WinAcmeGui.Domain.Renewals;

namespace WinAcmeGui.Application.Inventory;

public sealed record InstallationInventory(
    WinAcmeInstallation Installation,
    ConfigurationSnapshot Configuration,
    IReadOnlyList<Renewal> Renewals,
    IReadOnlyList<RenewalDiagnostic> Diagnostics);

public sealed class InventoryService(
    IWinAcmeConfigurationReader configurationReader,
    IRenewalReader renewalReader)
{
    public async Task<InstallationInventory> LoadAsync(WinAcmeInstallation installation, CancellationToken cancellationToken)
        => await LoadAsync(installation, null, cancellationToken);

    public async Task<InstallationInventory> LoadAsync(
        WinAcmeInstallation installation,
        ConfigurationSnapshot? resolvedConfiguration,
        CancellationToken cancellationToken)
    {
        var configuration = resolvedConfiguration ?? await configurationReader.ReadAsync(installation.ExecutablePath, cancellationToken);
        if (!PathsEqual(configuration.ConfigurationPath, installation.ConfigurationPath))
            throw new InvalidDataException("The installation configuration changed after discovery; reload before operating.");
        var results = await renewalReader.ReadDirectoryAsync(configuration.ConfigurationPath, cancellationToken);
        var renewals = results.Select(x => x.ToDisplayRenewal()).ToArray();
        var diagnostics = results.SelectMany(x => x.Diagnostics).ToArray();
        return new(installation, configuration, renewals, diagnostics);
    }

    private static bool PathsEqual(string left, string right)
    {
        // Malformed configuration paths must fail the comparison (and surface the reload
        // diagnostic) instead of throwing raw ArgumentException out of GetFullPath.
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        try
        {
            return Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Equals(Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            return false;
        }
    }
}
