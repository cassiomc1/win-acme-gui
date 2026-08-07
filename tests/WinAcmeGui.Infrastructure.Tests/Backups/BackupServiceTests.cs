using FluentAssertions;
using WinAcmeGui.Infrastructure.Backups;

namespace WinAcmeGui.Infrastructure.Tests.Backups;

public sealed class BackupServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "win-acme-gui-backups", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Creates_timestamped_backup_and_manifest()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "settings.json");
        await File.WriteAllTextAsync(source, "{\"PageSize\":50}");
        var service = new BackupService(Path.Combine(_root, "backups"));

        var result = await service.CreateAsync(source, "settings-update", CancellationToken.None);

        File.Exists(result.BackupPath).Should().BeTrue();
        File.Exists(result.ManifestPath).Should().BeTrue();
        (await File.ReadAllTextAsync(result.ManifestPath)).Should().Contain("settings-update");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
