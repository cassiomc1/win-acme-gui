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
            foreach (var logPath in logPaths.Where(File.Exists).Take(20))
            {
                var name = Path.GetFileName(logPath);
                var content = await File.ReadAllTextAsync(logPath, cancellationToken);
                content = Redact(content);
                content = RemovePrivateKeyBlocks(content);
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
        var begin = value.IndexOf("-----BEGIN ", StringComparison.OrdinalIgnoreCase);
        while (begin >= 0)
        {
            var end = value.IndexOf("-----END ", begin, StringComparison.OrdinalIgnoreCase);
            if (end < 0) return value[..begin] + "[private key removed]";
            var endLine = value.IndexOf('\n', end);
            value = value.Remove(begin, (endLine < 0 ? value.Length : endLine + 1) - begin).Insert(begin, "[private key removed]\n");
            begin = value.IndexOf("-----BEGIN ", begin + 1, StringComparison.OrdinalIgnoreCase);
        }
        return value;
    }
}
