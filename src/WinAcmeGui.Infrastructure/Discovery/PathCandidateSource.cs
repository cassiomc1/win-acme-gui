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
        var candidates = path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(directory => executableNames.Select(name => Path.Combine(directory, name)))
            .Where(File.Exists)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return Task.FromResult<IReadOnlyCollection<string>>(candidates);
    }
}
