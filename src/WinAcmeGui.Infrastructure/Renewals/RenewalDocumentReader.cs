using System.Text.Json;
using WinAcmeGui.Application.Renewals;
using WinAcmeGui.Domain.Renewals;

namespace WinAcmeGui.Infrastructure.Renewals;

public sealed class RenewalDocumentReader : IRenewalReader
{
    private static readonly HashSet<string> KnownSourcePlugins = new(StringComparer.OrdinalIgnoreCase)
    {
        "Manual", "IIS", "IISSite", "IISBinding", "Csr"
    };

    private static readonly string[] ModernSections =
    [
        "TargetPluginOptions", "ValidationPluginOptions", "CsrPluginOptions",
        "OrderPluginOptions", "StorePluginOptions", "InstallationPluginOptions"
    ];

    public async Task<RenewalReadResult> ReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Renewal JSON root must be an object.");

            var id = GetString(root, "Id") ?? GetFallbackId(path);
            var name = GetString(root, "LastFriendlyName") ?? GetString(root, "Name") ?? id;
            var domains = ReadDomains(root);
            var sourcePlugin = GetNestedString(root, "Plugin", "Source", "Plugin");
            var diagnostics = new List<RenewalDiagnostic>();
            var editable = true;
            var hasModernStructure = ModernSections.Any(section => root.TryGetProperty(section, out _));
            var hasLegacyStructure = root.TryGetProperty("Plugin", out var legacyPlugin) && legacyPlugin.ValueKind == JsonValueKind.Object;
            if (!hasModernStructure && !hasLegacyStructure)
            {
                diagnostics.Add(new("renewal.json.incomplete", "The renewal document does not contain the required Id and Plugin structure.", true));
                editable = false;
            }
            if (sourcePlugin is not null && !KnownSourcePlugins.Contains(sourcePlugin))
            {
                diagnostics.Add(new("renewal.plugin.unknown", $"Source plugin '{sourcePlugin}' is not supported by this GUI."));
                editable = false;
            }

            var renewal = new Renewal(
                id,
                name,
                domains,
                editable ? ReadStatus(root) : RenewalStatus.Unreadable,
                editable,
                path,
                diagnostics);
            return new(path, true, editable, renewal, diagnostics);
        }
        catch (OperationCanceledException) { throw; }
        catch (JsonException ex)
        {
            return RenewalReadResult.Invalid(path, new RenewalDiagnostic("renewal.json.invalid", ex.Message, true));
        }
        catch (InvalidDataException ex)
        {
            return RenewalReadResult.Invalid(path, new RenewalDiagnostic("renewal.json.invalid", ex.Message, true));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return RenewalReadResult.Invalid(path, new RenewalDiagnostic("renewal.file.unreadable", ex.Message, true));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or NotSupportedException)
        {
            return RenewalReadResult.Invalid(path, new RenewalDiagnostic("renewal.json.invalid", ex.Message, true));
        }
    }

    public async Task<IReadOnlyList<RenewalReadResult>> ReadDirectoryAsync(string configurationPath, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(configurationPath)) return [];
        string[] paths;
        try
        {
            paths = Directory.EnumerateFiles(configurationPath, "*.renewal.json", SearchOption.TopDirectoryOnly).ToArray();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [RenewalReadResult.Invalid(configurationPath, new RenewalDiagnostic("renewal.directory.unreadable", ex.Message, true))];
        }

        var results = new List<RenewalReadResult>(paths.Length);
        foreach (var path in paths.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await ReadAsync(path, cancellationToken));
        }
        return results;
    }

    private static IReadOnlyList<string> ReadDomains(JsonElement root)
    {
        var source = GetNestedObject(root, "Plugin", "Source");
        if (source is null) return [];
        var values = new List<string>();
        var main = GetString(source.Value, "MainDomain");
        if (!string.IsNullOrWhiteSpace(main)) values.Add(main);
        if (source.Value.TryGetProperty("AltNames", out var altNames) && altNames.ValueKind == JsonValueKind.Array)
            values.AddRange(altNames.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!));
        return values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static RenewalStatus ReadStatus(JsonElement root)
    {
        if (!root.TryGetProperty("History", out var history) || history.ValueKind != JsonValueKind.Array) return RenewalStatus.Healthy;
        var entries = history.EnumerateArray()
            .Select((entry, index) => new { Entry = entry, Index = index })
            .Where(x => x.Entry.ValueKind == JsonValueKind.Object)
            .Select(x => new
            {
                x.Index,
                Success = GetBoolean(x.Entry, "Success"),
                ValidTo = GetDateTime(x.Entry, "ValidTo") ?? GetOrderExpireDate(x.Entry),
                Timestamp = GetDateTime(x.Entry, "Date")
                    ?? GetDateTime(x.Entry, "Created")
                    ?? GetDateTime(x.Entry, "ValidFrom")
                    ?? GetDateTime(x.Entry, "ValidTo")
            })
            .OrderBy(x => x.Timestamp ?? DateTimeOffset.MinValue)
            .ThenBy(x => x.Index)
            .ToArray();
        var latest = entries.LastOrDefault();
        var success = latest?.Success;
        var validTo = latest?.ValidTo;

        if (success == false) return RenewalStatus.Failed;
        if (validTo is null) return RenewalStatus.Healthy;
        if (validTo <= DateTimeOffset.UtcNow) return RenewalStatus.Expired;
        return validTo <= DateTimeOffset.UtcNow.AddDays(30) ? RenewalStatus.DueSoon : RenewalStatus.Healthy;
    }

    private static DateTimeOffset? GetOrderExpireDate(JsonElement entry)
    {
        if (!entry.TryGetProperty("OrderResults", out var orders) || orders.ValueKind != JsonValueKind.Array) return null;
        return orders.EnumerateArray()
            .Select(order => GetDateTime(order, "ExpireDate"))
            .Where(x => x is not null)
            .Select(x => x!.Value)
            .OrderByDescending(x => x)
            .Cast<DateTimeOffset?>()
            .FirstOrDefault();
    }

    private static JsonElement? GetNestedObject(JsonElement root, params string[] properties)
    {
        var current = root;
        foreach (var property in properties)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(property, out current)) return null;
        }
        return current.ValueKind == JsonValueKind.Object ? current : null;
    }

    private static string? GetNestedString(JsonElement root, params string[] properties)
    {
        var parent = GetNestedObject(root, properties[..^1]);
        return parent is { } value ? GetString(value, properties[^1]) : null;
    }

    private static string? GetString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool? GetBoolean(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(property, out var value)
        && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static DateTimeOffset? GetDateTime(JsonElement element, string property)
    {
        var value = GetString(element, property);
        // wacs writes ISO-8601 timestamps; parse them culture-independently so a machine with a
        // non-standard calendar or culture cannot misread renewal health.
        return DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var parsed) ? parsed : null;
    }

    private static string GetFallbackId(string path) =>
        Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path));
}
