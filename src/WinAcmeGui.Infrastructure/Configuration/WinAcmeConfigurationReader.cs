using System.Diagnostics;
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
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(30);

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
        using var probeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        probeCancellation.CancelAfter(ProbeTimeout);
        var outputTask = process.StandardOutput.ReadToEndAsync(probeCancellation.Token);
        var errorTask = process.StandardError.ReadToEndAsync(probeCancellation.Token);
        try
        {
            await process.WaitForExitAsync(probeCancellation.Token);
            var output = await outputTask;
            var error = await errorTask;
            if (process.ExitCode != 0) throw new InvalidOperationException($"wacs.exe --version failed: {error.Trim()}");
            return string.IsNullOrWhiteSpace(output) ? error.Trim() : output.Trim();
        }
        catch (OperationCanceledException)
        {
            await TerminateAsync(process);
            throw;
        }
    }

    private static async Task TerminateAsync(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
        try
        {
            using var waitCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(waitCancellation.Token);
        }
        catch (OperationCanceledException) { }
        catch (InvalidOperationException) { }
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
        var clientName = NormalizeClientName(GetString(root, "Client", "ClientName"));
        var configuredPath = GetString(root, "Client", "ConfigurationPath");
        var uriText = GetString(root, "ACME", "DefaultBaseUri") ?? "https://acme-v02.api.letsencrypt.org/";
        if (!Uri.TryCreate(uriText, UriKind.Absolute, out var uri))
            throw new InvalidDataException($"Invalid ACME endpoint in {settingsPath}.");
        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The ACME endpoint must use HTTPS.");

        var installationDirectory = Path.GetDirectoryName(Path.GetFullPath(executablePath))!;
        var configurationPath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), clientName, uri.Host))
            : ResolveConfigurationPath(configuredPath, installationDirectory);

        document?.Dispose();
        return new(settingsPath, clientName, configurationPath, new AcmeEndpoint(uri, IsProductionEndpoint(uri)),
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

    private static string NormalizeClientName(string? value)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? "win-acme" : value.Trim();
        return candidate is "." or ".."
            || candidate.Contains('/')
            || candidate.Contains('\\')
            || candidate.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            ? "win-acme"
            : candidate;
    }

    private static string ResolveConfigurationPath(string configuredPath, string installationDirectory)
    {
        if (LooksLikeWindowsAbsolutePath(configuredPath) && !OperatingSystem.IsWindows()) return configuredPath;
        return Path.IsPathFullyQualified(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : Path.GetFullPath(Path.Combine(installationDirectory, configuredPath));
    }

    private static bool LooksLikeWindowsAbsolutePath(string path) =>
        path.Length >= 3
        && char.IsLetter(path[0])
        && path[1] == ':'
        && (path[2] == '\\' || path[2] == '/');

    private static bool IsProductionEndpoint(Uri uri) =>
        uri.Host.Equals("acme-v02.api.letsencrypt.org", StringComparison.OrdinalIgnoreCase);
}
