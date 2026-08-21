using WinAcmeGui.Application.Discovery;

namespace WinAcmeGui.Infrastructure.Discovery;

public sealed class PathCandidateSource(Func<string?>? pathProvider = null) : IInstallationCandidateSource
{
    public Task<IReadOnlyCollection<string>> FindAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = pathProvider?.Invoke() ?? Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path)) return Task.FromResult<IReadOnlyCollection<string>>([]);

        var executableNames = OperatingSystem.IsWindows() ? new[] { "wacs.exe", "wacs" } : new[] { "wacs", "wacs.exe" };
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var seen = new HashSet<string>(comparer);
        var candidates = new List<string>();
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var name in executableNames)
            {
                string combined;
                try
                {
                    // A single hostile/corrupt PATH entry (embedded NUL, invalid drive) must not abort discovery.
                    combined = Path.GetFullPath(Path.Combine(directory, name));
                    if (!File.Exists(combined) || !seen.Add(combined)) continue;
                    candidates.Add(combined);
                }
                catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
                {
                    continue;
                }
            }
        }
        return Task.FromResult<IReadOnlyCollection<string>>(candidates);
    }
}
