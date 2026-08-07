using System.Diagnostics;
using WinAcmeGui.Application.Discovery;

namespace WinAcmeGui.Infrastructure.Discovery;

public sealed class ProcessCandidateSource : IInstallationCandidateSource
{
    public Task<IReadOnlyCollection<string>> FindAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var paths = new List<string>();
        foreach (var process in Process.GetProcessesByName("wacs"))
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(process.MainModule?.FileName)) paths.Add(process.MainModule.FileName);
            }
            catch (Exception) when (process.HasExited || process.MainModule is null) { }
            finally { process.Dispose(); }
        }
        return Task.FromResult<IReadOnlyCollection<string>>(paths);
    }
}
