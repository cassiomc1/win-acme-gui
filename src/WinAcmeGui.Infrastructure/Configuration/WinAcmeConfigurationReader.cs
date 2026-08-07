using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using WinAcmeGui.Application.Configuration;
using WinAcmeGui.Domain.Installations;

namespace WinAcmeGui.Infrastructure.Configuration;

public interface IWinAcmeVersionProbe
{
    Task<string> GetVersionAsync(string executablePath, CancellationToken cancellationToken);
}

public sealed class ProcessWinAcmeVersionProbe : IWinAcmeVersionProbe
{
    public async Task<string> GetVersionAsync(string executablePath, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(executablePath, "--version")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start wacs.exe.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = await outputTask;
        var error = await errorTask;
        if (process.ExitCode != 0) throw new InvalidOperationException($"wacs.exe --version failed: {error.Trim()}");
        return string.IsNullOrWhiteSpace(output) ? error.Trim() : output.Trim();
    }
}

public sealed class WinAcmeConfigurationReader(IWinAcmeVersionProbe versionProbe) : IWinAcmeConfigurationReader
{
    public async Task<ConfigurationSnapshot> ReadAsync(string executablePath, CancellationToken cancellationToken)
    {
        _ = await versionProbe.GetVersionAsync(executablePath, cancellationToken);
        var settingsPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(executablePath))!, "settings.json");
        JsonDocument? document = null;
        if (File.Exists(settingsPath))
        {
            await using var stream = File.Open(settingsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }

        var root = document?.RootElement;
        var clientName = GetString(root, "Client", "ClientName") ?? "win-acme";
        var configuredPath = GetString(root, "Client", "ConfigurationPath");
        var uriText = GetString(root, "ACME", "DefaultBaseUri") ?? "https://acme-v02.api.letsencrypt.org/";
        if (!Uri.TryCreate(uriText, UriKind.Absolute, out var uri))
            throw new InvalidDataException($"Invalid ACME endpoint in {settingsPath}.");

        var configurationPath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), clientName, uri.Host)
            : configuredPath;

        document?.Dispose();
        return new(settingsPath, clientName, configurationPath, new AcmeEndpoint(uri, !uri.Host.Contains("staging", StringComparison.OrdinalIgnoreCase)),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ClientName"] = clientName,
                ["ConfigurationPath"] = configurationPath,
                ["DefaultBaseUri"] = uri.ToString()
            });
    }

    private static string? GetString(JsonElement? root, string section, string property)
    {
        if (root is not { } value || value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(section, out var sectionValue)) return null;
        if (!sectionValue.TryGetProperty(property, out var propertyValue) || propertyValue.ValueKind == JsonValueKind.Null) return null;
        return propertyValue.ValueKind == JsonValueKind.String ? propertyValue.GetString() : propertyValue.ToString();
    }
}
