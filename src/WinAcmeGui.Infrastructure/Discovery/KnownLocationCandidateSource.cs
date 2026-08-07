using WinAcmeGui.Application.Discovery;

namespace WinAcmeGui.Infrastructure.Discovery;

public sealed class KnownLocationCandidateSource(string? appDirectory = null) : IInstallationCandidateSource
{
    public Task<IReadOnlyCollection<string>> FindAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var roots = new[]
        {
            appDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase);

        var candidates = roots.SelectMany(root => new[]
            {
                Path.Combine(root!, "win-acme", "wacs.exe"),
                Path.Combine(root!, "win-acme", "wacs"),
                Path.Combine(root!, "wacs.exe"),
                Path.Combine(root!, "wacs")
            })
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return Task.FromResult<IReadOnlyCollection<string>>(candidates);
    }
}
