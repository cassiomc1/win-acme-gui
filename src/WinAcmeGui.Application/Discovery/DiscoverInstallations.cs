using WinAcmeGui.Domain.Installations;

namespace WinAcmeGui.Application.Discovery;

using WinAcmeGui.Application.Configuration;

public interface IInstallationCandidateSource
{
    Task<IReadOnlyCollection<string>> FindAsync(CancellationToken cancellationToken);
}

public interface IInstallationValidator
{
    Task<InstallationCandidate?> ValidateAsync(string executablePath, CancellationToken cancellationToken);
}

public sealed record InstallationCandidate(
    string ExecutablePath,
    string VersionText,
    string ConfigurationPath,
    AcmeEndpoint? Endpoint = null,
    ConfigurationSnapshot? Configuration = null,
    bool IsOperational = true,
    string? Diagnostic = null);

public sealed record DiscoveryDiagnostic(string Code, string Message);

public sealed record DiscoveryResult(
    IReadOnlyList<InstallationCandidate> Installations,
    IReadOnlyList<DiscoveryDiagnostic> Diagnostics);

public sealed class DiscoverInstallations(
    IReadOnlyCollection<IInstallationCandidateSource> sources,
    IInstallationValidator validator)
{
    public async Task<DiscoveryResult> ExecuteAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var diagnostics = new List<DiscoveryDiagnostic>();
        var candidates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            try
            {
                foreach (var path in await source.FindAsync(cancellationToken))
                {
                    var key = Canonicalize(path);
                    candidates.TryAdd(key, path);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                diagnostics.Add(new("discovery.source.failed", ex.Message));
            }
        }

        var valid = new List<InstallationCandidate>();
        foreach (var path in candidates.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(path);
            var candidate = await validator.ValidateAsync(path, cancellationToken);
            if (candidate is not null) valid.Add(candidate);
        }

        foreach (var collision in valid
            .GroupBy(x => Canonicalize(x.ConfigurationPath), StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1))
        {
            diagnostics.Add(new(
                "discovery.configuration.collision",
                $"Multiple win-acme executables resolve to the same configuration directory: {collision.Key}"));
            var message = "This installation is read-only because its configuration path is shared by another discovered executable.";
            for (var index = 0; index < valid.Count; index++)
            {
                if (collision.Contains(valid[index]))
                    valid[index] = valid[index] with { IsOperational = false, Diagnostic = message };
            }
        }

        return new(valid, diagnostics);
    }

    private static string Canonicalize(string path)
    {
        var normalized = path.Replace('/', '\\');
        var segments = new List<string>();
        foreach (var segment in normalized.Split('\\', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".") continue;
            if (segment == ".." && segments.Count > 0)
            {
                segments.RemoveAt(segments.Count - 1);
                continue;
            }
            segments.Add(segment);
        }
        return string.Join('\\', segments).TrimEnd('\\').ToUpperInvariant();
    }
}
