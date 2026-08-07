using FluentAssertions;
using WinAcmeGui.Infrastructure.Discovery;

namespace WinAcmeGui.Infrastructure.Tests.Discovery;

public sealed class PathCandidateSourceTests
{
    [Fact]
    public async Task Finds_wacs_from_path_entries()
    {
        var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        await File.WriteAllTextAsync(Path.Combine(temp, OperatingSystem.IsWindows() ? "wacs.exe" : "wacs"), "fixture");
        var source = new PathCandidateSource(() => temp);

        var paths = await source.FindAsync(CancellationToken.None);

        paths.Should().ContainSingle();
        Directory.Delete(temp, true);
    }
}
