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
                // MainModule access commonly fails for elevated/foreign processes; capture it once so
                // neither the null check nor the filter re-throw can crash the whole discovery pass.
                var fileName = process.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(fileName)) paths.Add(fileName);
            }
            catch (Exception)
            {
                // Inaccessible process: skip it and keep scanning.
            }
            finally { process.Dispose(); }
        }
        return Task.FromResult<IReadOnlyCollection<string>>(paths);
    }
}
