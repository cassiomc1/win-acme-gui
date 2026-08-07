using System.Text.Json.Nodes;
using FluentAssertions;
using WinAcmeGui.Infrastructure.Backups;
using WinAcmeGui.Infrastructure.Configuration;

namespace WinAcmeGui.Infrastructure.Tests.Configuration;

public sealed class WinAcmeSettingsWriterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "win-acme-gui-settings-write", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Update_preserves_unknown_properties_and_creates_backup_first()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "settings.json");
        await File.WriteAllTextAsync(path, "{\"UI\":{\"PageSize\":50},\"FutureOption\":{\"Enabled\":true}}");
        var writer = new WinAcmeSettingsWriter(new BackupService(Path.Combine(_root, "backups")));

        var result = await writer.UpdateAsync(path, new SettingsPatch(PageSize: 100), CancellationToken.None);

        result.BackupPath.Should().NotBeNullOrWhiteSpace();
        var document = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
        document["FutureOption"]!["Enabled"]!.GetValue<bool>().Should().BeTrue();
        document["UI"]!["PageSize"]!.GetValue<int>().Should().Be(100);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
