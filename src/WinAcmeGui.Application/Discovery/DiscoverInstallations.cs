using WinAcmeGui.Domain.Installations;

namespace WinAcmeGui.Application.Discovery;

public interface IInstallationCandidateSource
{
    Task<IReadOnlyCollection<string>> FindAsync(CancellationToken cancellationToken);
}

public interface IInstallationValidator
{
    Task<InstallationCandidate?> ValidateAsync(string executablePath, CancellationToken cancellationToken);
}

public sealed record InstallationCandidate(string ExecutablePath, string VersionText, string ConfigurationPath);

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
