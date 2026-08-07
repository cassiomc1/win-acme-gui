using System.Text.Json;
using WinAcmeGui.Application.Renewals;
using WinAcmeGui.Domain.Renewals;

namespace WinAcmeGui.Infrastructure.Renewals;

public sealed class RenewalDocumentReader : IRenewalReader
{
    private static readonly HashSet<string> KnownSourcePlugins = new(StringComparer.OrdinalIgnoreCase) { "Manual", "IIS", "IISSite", "IISBinding" };

    public async Task<RenewalReadResult> ReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;
            var id = GetString(root, "Id") ?? Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path));
            var name = GetString(root, "Name") ?? id;
            var domains = ReadDomains(root);
            var sourcePlugin = root.TryGetProperty("Plugin", out var plugin) && plugin.TryGetProperty("Source", out var source)
                ? GetString(source, "Plugin") : null;
            var diagnostics = new List<RenewalDiagnostic>();
            var editable = true;
            if (sourcePlugin is not null && !KnownSourcePlugins.Contains(sourcePlugin))
            {
                diagnostics.Add(new("renewal.plugin.unknown", $"Source plugin '{sourcePlugin}' is not supported by this GUI."));
                editable = false;
            }

            var renewal = new Renewal(id, name, domains, RenewalStatus.Healthy, editable, path, diagnostics);
            return new(path, true, editable, renewal, diagnostics);
        }
        catch (JsonException ex)
        {
            return RenewalReadResult.Invalid(path, new RenewalDiagnostic("renewal.json.invalid", ex.Message, true));
        }
        catch (IOException ex)
        {
            return RenewalReadResult.Invalid(path, new RenewalDiagnostic("renewal.file.unreadable", ex.Message, true));
        }
    }

    public async Task<IReadOnlyList<RenewalReadResult>> ReadDirectoryAsync(string configurationPath, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(configurationPath)) return [];
        var paths = Directory.EnumerateFiles(configurationPath, "*.renewal.json", SearchOption.TopDirectoryOnly).ToArray();
        var results = new List<RenewalReadResult>(paths.Length);
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await ReadAsync(path, cancellationToken));
        }
        return results;
    }

    private static IReadOnlyList<string> ReadDomains(JsonElement root)
    {
        if (!root.TryGetProperty("Plugin", out var plugin) || !plugin.TryGetProperty("Source", out var source)) return [];
        var values = new List<string>();
        var main = GetString(source, "MainDomain");
        if (!string.IsNullOrWhiteSpace(main)) values.Add(main);
        if (source.TryGetProperty("AltNames", out var altNames) && altNames.ValueKind == JsonValueKind.Array)
            values.AddRange(altNames.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).Where(x => x.Length > 0));
        return values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
