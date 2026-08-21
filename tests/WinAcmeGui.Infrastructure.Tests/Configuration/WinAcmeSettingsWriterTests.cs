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

    [Fact]
    public async Task Non_object_settings_reject_with_a_clear_error()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "settings.json");
        await File.WriteAllTextAsync(path, "[1, 2, 3]");
        var writer = new WinAcmeSettingsWriter(new BackupService(Path.Combine(_root, "backups")));

        var act = () => writer.UpdateAsync(path, new SettingsPatch(PageSize: 100), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*must contain an object*");
    }

    [Fact]
    public async Task Rejected_patches_do_not_create_orphan_backups()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "settings.json");
        await File.WriteAllTextAsync(path, "{\"UI\":{\"PageSize\":50}}");
        var backupRoot = Path.Combine(_root, "backups");
        var writer = new WinAcmeSettingsWriter(new BackupService(backupRoot));

        var act = () => writer.UpdateAsync(path, new SettingsPatch(RenewalDays: 9999), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        if (Directory.Exists(backupRoot))
            Directory.EnumerateFiles(backupRoot, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
