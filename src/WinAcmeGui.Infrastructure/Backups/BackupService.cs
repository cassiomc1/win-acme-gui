using System.Security.Cryptography;
using System.Text.Json;

namespace WinAcmeGui.Infrastructure.Backups;

public sealed record BackupResult(string BackupPath, string ManifestPath, string Sha256);

public sealed class BackupService(string backupRoot)
{
    public async Task<BackupResult> CreateAsync(string sourcePath, string operationId, CancellationToken cancellationToken)
    {
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Backup source not found.", sourcePath);
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        var directory = Path.Combine(backupRoot, stamp + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var backupPath = Path.Combine(directory, Path.GetFileName(sourcePath));
        await using (var source = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        await using (var destination = File.Open(backupPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await source.CopyToAsync(destination, cancellationToken);

        await using var backup = File.OpenRead(backupPath);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(backup, cancellationToken));
        var manifestPath = Path.Combine(directory, "manifest.json");
        var manifest = new
        {
            SourcePath = sourcePath,
            BackupPath = backupPath,
            OperationId = operationId,
            CreatedUtc = DateTimeOffset.UtcNow,
            Sha256 = hash
        };
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        return new(backupPath, manifestPath, hash);
    }
}
