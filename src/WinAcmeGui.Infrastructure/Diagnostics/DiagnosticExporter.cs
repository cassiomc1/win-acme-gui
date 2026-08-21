using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace WinAcmeGui.Infrastructure.Diagnostics;

public sealed class DiagnosticExporter(IEnumerable<string> secrets)
{
    private readonly string[] _secrets = secrets.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

    public async Task ExportAsync(
        string destinationZip,
        IReadOnlyDictionary<string, string> metadata,
        IReadOnlyCollection<string> logPaths,
        CancellationToken cancellationToken)
    {
        var temp = Path.Combine(Path.GetTempPath(), "win-acme-gui-diagnostic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var safeMetadata = metadata.ToDictionary(x => x.Key, x => Redact(x.Value));
            await File.WriteAllTextAsync(Path.Combine(temp, "metadata.json"), JsonSerializer.Serialize(safeMetadata, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
            var logs = Path.Combine(temp, "logs");
            Directory.CreateDirectory(logs);
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var logPath in logPaths.Where(File.Exists).Take(20))
            {
                var content = await File.ReadAllTextAsync(logPath, cancellationToken);
                content = Redact(content);
                content = RemovePrivateKeyBlocks(content);
                // Two logs from different directories can share a filename; index the collision
                // instead of silently destroying one of the exported artifacts.
                var name = Path.GetFileName(logPath);
                if (!usedNames.Add(name))
                {
                    var stem = Path.GetFileNameWithoutExtension(logPath);
                    var extension = Path.GetExtension(logPath);
                    var index = 1;
                    while (!usedNames.Add($"{stem}-{index:000}{extension}")) index++;
                    name = $"{stem}-{index:000}{extension}";
                }
                await File.WriteAllTextAsync(Path.Combine(logs, name), content, Encoding.UTF8, cancellationToken);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destinationZip))!);
            if (File.Exists(destinationZip)) File.Delete(destinationZip);
            ZipFile.CreateFromDirectory(temp, destinationZip, CompressionLevel.Optimal, false);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    private string Redact(string value)
    {
        foreach (var secret in _secrets) value = value.Replace(secret, "••••••••", StringComparison.Ordinal);
        return value;
    }

    private static string RemovePrivateKeyBlocks(string value)
    {
        // Only private key material is removed; public certificates and public keys keep their
        // diagnostic value and are safe to share.
        var begin = value.IndexOf("-----BEGIN ", StringComparison.OrdinalIgnoreCase);
        while (begin >= 0)
        {
            var labelEnd = value.IndexOf('\n', begin);
            var header = labelEnd < 0 ? value[begin..] : value[begin..labelEnd];
            var isPrivate = header.Contains("PRIVATE KEY", StringComparison.OrdinalIgnoreCase);
            var end = value.IndexOf("-----END ", begin, StringComparison.OrdinalIgnoreCase);
            if (end < 0) return isPrivate ? value[..begin] + "[private key removed]" : value;
            var endLine = value.IndexOf('\n', end);
            if (isPrivate)
            {
                value = value.Remove(begin, (endLine < 0 ? value.Length : endLine + 1) - begin).Insert(begin, "[private key removed]\n");
            }
            begin = value.IndexOf("-----BEGIN ", begin + 1, StringComparison.OrdinalIgnoreCase);
        }
        return value;
    }
}
