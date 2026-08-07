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
    {
        var configuration = await configurationReader.ReadAsync(installation.ExecutablePath, cancellationToken);
        var results = await renewalReader.ReadDirectoryAsync(configuration.ConfigurationPath, cancellationToken);
        var renewals = results.Where(x => x.Renewal is not null).Select(x => x.Renewal!).ToArray();
        var diagnostics = results.SelectMany(x => x.Diagnostics).ToArray();
        return new(installation, configuration, renewals, diagnostics);
    }
}
