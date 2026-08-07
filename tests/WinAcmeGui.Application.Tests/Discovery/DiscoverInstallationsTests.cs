using FluentAssertions;
using WinAcmeGui.Application.Discovery;

namespace WinAcmeGui.Application.Tests.Discovery;

public sealed class DiscoverInstallationsTests
{
    [Fact]
    public async Task Deduplicates_candidates_by_canonical_executable_path()
    {
        var sources = new IInstallationCandidateSource[]
        {
            new StubSource(@"C:\Tools\wacs.exe"),
            new StubSource(@"C:\Tools\.\wacs.exe")
        };
        var validator = new StubValidator();

        var result = await new DiscoverInstallations(sources, validator).ExecuteAsync(null, CancellationToken.None);

        result.Installations.Should().ContainSingle();
    }

    [Fact]
    public async Task Returns_partial_results_when_one_source_fails()
    {
        var sources = new IInstallationCandidateSource[]
        {
            new ThrowingSource(),
            new StubSource(@"C:\Tools\wacs.exe")
        };
        var result = await new DiscoverInstallations(sources, new StubValidator()).ExecuteAsync(null, CancellationToken.None);

        result.Installations.Should().ContainSingle();
        result.Diagnostics.Should().Contain(x => x.Code == "discovery.source.failed");
    }

    private sealed class StubSource(string path) : IInstallationCandidateSource
    {
        public Task<IReadOnlyCollection<string>> FindAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<string>>([path]);
    }

    private sealed class ThrowingSource : IInstallationCandidateSource
    {
        public Task<IReadOnlyCollection<string>> FindAsync(CancellationToken cancellationToken) => throw new IOException("fixture failure");
    }

    private sealed class StubValidator : IInstallationValidator
    {
        public Task<InstallationCandidate?> ValidateAsync(string executablePath, CancellationToken cancellationToken) =>
            Task.FromResult<InstallationCandidate?>(new InstallationCandidate(executablePath, "2.2.9.1", executablePath));
    }
}
