using System.Text.Json;
using System.Text.Json.Nodes;
using WinAcmeGui.Infrastructure.Backups;

namespace WinAcmeGui.Infrastructure.Configuration;

public sealed record SettingsPatch(
    int? PageSize = null,
    int? RenewalDays = null,
    int? RenewalDaysRange = null,
    bool? VersionCheck = null,
    string? StartBoundary = null);

public sealed record SettingsWriteResult(string BackupPath, string ManifestPath);

public sealed class WinAcmeSettingsWriter(BackupService backupService)
{
    public async Task<SettingsWriteResult> UpdateAsync(string settingsPath, SettingsPatch patch, CancellationToken cancellationToken)
    {
        var backup = await backupService.CreateAsync(settingsPath, "settings-update", cancellationToken);
        var root = JsonNode.Parse(await File.ReadAllTextAsync(settingsPath, cancellationToken))?.AsObject()
            ?? throw new InvalidDataException("settings.json must contain an object.");
        var ui = EnsureObject(root, "UI");
        var task = EnsureObject(root, "ScheduledTask");
        if (patch.PageSize is { } pageSize) { if (pageSize is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(patch.PageSize)); ui["PageSize"] = pageSize; }
        if (patch.RenewalDays is { } renewalDays) { if (renewalDays is < 1 or > 90) throw new ArgumentOutOfRangeException(nameof(patch.RenewalDays)); task["RenewalDays"] = renewalDays; }
        if (patch.RenewalDaysRange is { } range) { if (range < 0) throw new ArgumentOutOfRangeException(nameof(patch.RenewalDaysRange)); task["RenewalDaysRange"] = range; }
        if (patch.VersionCheck is { } versionCheck) root["VersionCheck"] = versionCheck;
        if (patch.StartBoundary is { } startBoundary) task["StartBoundary"] = startBoundary;

        var temp = settingsPath + ".win-acme-gui.tmp";
        await File.WriteAllTextAsync(temp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        try
        {
            try { File.Replace(temp, settingsPath, null); }
            catch (PlatformNotSupportedException) { File.Move(temp, settingsPath, true); }
            catch (IOException) { File.Move(temp, settingsPath, true); }
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
        return new(backup.BackupPath, backup.ManifestPath);
    }

    private static JsonObject EnsureObject(JsonObject root, string name)
    {
        if (root[name] is JsonObject value) return value;
        value = new JsonObject();
        root[name] = value;
        return value;
    }
}
