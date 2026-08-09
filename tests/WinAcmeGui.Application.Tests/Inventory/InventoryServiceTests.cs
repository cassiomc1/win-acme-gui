using FluentAssertions;
using WinAcmeGui.Application.Configuration;
using WinAcmeGui.Application.Inventory;
using WinAcmeGui.Application.Renewals;
using WinAcmeGui.Domain.Installations;
using WinAcmeGui.Domain.Renewals;

namespace WinAcmeGui.Application.Tests.Inventory;

public sealed class InventoryServiceTests
{
    [Fact]
    public async Task Loads_current_configuration_and_renewals_without_editing_files()
    {
        var root = Path.Combine(Path.GetTempPath(), "win-acme-gui-inventory");
        var snapshot = new ConfigurationSnapshot(
            Path.Combine(root, "settings.json"),
            "win-acme",
            Path.Combine(root, "config"),
            AcmeEndpoint.Production,
            new Dictionary<string, string>());
        var installation = WinAcmeInstallation.Create(Path.Combine(root, "wacs.exe"), new(2, 2, 9, 1), snapshot.ConfigurationPath, snapshot.Endpoint);
        var service = new InventoryService(
            new StubConfigurationReader(snapshot),
            new StubRenewalReader([
                new RenewalReadResult(@"C:\renewal.renewal.json", true, true,
                    new Renewal("id", "example.com", ["example.com"], RenewalStatus.Healthy, true, "file", []), [])]));

        var result = await service.LoadAsync(installation, CancellationToken.None);

        result.Installation.Should().Be(installation);
        result.Renewals.Should().ContainSingle(x => x.FriendlyName == "example.com");
    }

    [Fact]
    public async Task Keeps_unreadable_renewal_visible_with_diagnostic_status()
    {
        var root = Path.Combine(Path.GetTempPath(), "win-acme-gui-inventory-invalid");
        var snapshot = new ConfigurationSnapshot(
            Path.Combine(root, "settings.json"),
            "win-acme",
            Path.Combine(root, "config"),
            AcmeEndpoint.Production,
            new Dictionary<string, string>());
        var installation = WinAcmeInstallation.Create(Path.Combine(root, "wacs.exe"), new(2, 2, 9, 1), snapshot.ConfigurationPath, snapshot.Endpoint);
        var service = new InventoryService(
            new StubConfigurationReader(snapshot),
            new StubRenewalReader([
                RenewalReadResult.Invalid("broken.renewal.json", new RenewalDiagnostic("renewal.json.invalid", "broken", true))]));

        var result = await service.LoadAsync(installation, CancellationToken.None);

        result.Renewals.Should().ContainSingle(x => x.Status == RenewalStatus.Unreadable);
        result.Diagnostics.Should().ContainSingle(x => x.Code == "renewal.json.invalid");
    }

    [Fact]
    public async Task Rejects_a_stale_resolved_configuration_snapshot()
    {
        var root = Path.Combine(Path.GetTempPath(), "win-acme-gui-inventory-snapshot");
        var resolved = new ConfigurationSnapshot(
            Path.Combine(root, "settings.json"),
            "win-acme",
            Path.Combine(root, "resolved"),
            AcmeEndpoint.Production,
            new Dictionary<string, string>());
        var installation = WinAcmeInstallation.Create(Path.Combine(root, "wacs.exe"), new(2, 2, 9, 1), Path.Combine(root, "changed"), resolved.Endpoint);
        var service = new InventoryService(new StubConfigurationReader(resolved), new StubRenewalReader([]));

        var act = () => service.LoadAsync(installation, resolved, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    private sealed class StubConfigurationReader(ConfigurationSnapshot snapshot) : IWinAcmeConfigurationReader
    {
        public Task<ConfigurationSnapshot> ReadAsync(string executablePath, CancellationToken cancellationToken) => Task.FromResult(snapshot);
    }

    private sealed class StubRenewalReader(IReadOnlyList<RenewalReadResult> results) : IRenewalReader
    {
        public Task<IReadOnlyList<RenewalReadResult>> ReadDirectoryAsync(string configurationPath, CancellationToken cancellationToken) => Task.FromResult(results);
    }
}
