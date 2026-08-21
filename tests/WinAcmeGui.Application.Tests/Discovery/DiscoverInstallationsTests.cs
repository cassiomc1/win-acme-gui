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

    [Fact]
    public async Task Blocks_installations_that_share_a_configuration_path()
    {
        var sources = new IInstallationCandidateSource[]
        {
            new StubSource(@"C:\Tools\one\wacs.exe"),
            new StubSource(@"C:\Tools\two\wacs.exe")
        };

        var result = await new DiscoverInstallations(sources, new SharedConfigValidator()).ExecuteAsync(null, CancellationToken.None);

        result.Installations.Should().HaveCount(2).And.OnlyContain(x => !x.IsOperational);
        result.Diagnostics.Should().Contain(x => x.Code == "discovery.configuration.collision");
    }

    [Fact]
    public async Task Returns_partial_results_when_a_source_throws_any_exception_type()
    {
        var sources = new IInstallationCandidateSource[]
        {
            // Process.Start throws Win32Exception when schtasks.exe is missing or blocked; that
            // must degrade to a diagnostic, never abort the remaining sources.
            new ThrowingSource(() => new System.ComponentModel.Win32Exception(-2147467259, "The system cannot find the file specified.")),
            new StubSource(@"C:\Tools\wacs.exe")
        };
        var result = await new DiscoverInstallations(sources, new StubValidator()).ExecuteAsync(null, CancellationToken.None);

        result.Installations.Should().ContainSingle();
        result.Diagnostics.Should().Contain(x => x.Code == "discovery.source.failed");
    }

    [Fact]
    public async Task Does_not_flag_empty_configuration_paths_as_shared()
    {
        var sources = new IInstallationCandidateSource[]
        {
            new StubSource(@"C:\Tools\one\wacs.exe"),
            new StubSource(@"C:\Tools\two\wacs.exe")
        };

        var result = await new DiscoverInstallations(sources, new EmptyConfigPathValidator()).ExecuteAsync(null, CancellationToken.None);

        result.Installations.Should().HaveCount(2).And.OnlyContain(x => x.IsOperational);
        result.Diagnostics.Should().NotContain(x => x.Code == "discovery.configuration.collision");
    }

    private sealed class StubSource(string path) : IInstallationCandidateSource
    {
        public Task<IReadOnlyCollection<string>> FindAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<string>>([path]);
    }

    private sealed class ThrowingSource(Func<Exception>? exceptionFactory = null) : IInstallationCandidateSource
    {
        public Task<IReadOnlyCollection<string>> FindAsync(CancellationToken cancellationToken) =>
            throw exceptionFactory?.Invoke() ?? new IOException("fixture failure");
    }

    private sealed class StubValidator : IInstallationValidator
    {
        public Task<InstallationCandidate?> ValidateAsync(string executablePath, CancellationToken cancellationToken) =>
            Task.FromResult<InstallationCandidate?>(new InstallationCandidate(executablePath, "2.2.9.1", executablePath));
    }

    private sealed class SharedConfigValidator : IInstallationValidator
    {
        public Task<InstallationCandidate?> ValidateAsync(string executablePath, CancellationToken cancellationToken) =>
            Task.FromResult<InstallationCandidate?>(new InstallationCandidate(executablePath, "2.2.9.1", @"C:\Shared\config"));
    }

    private sealed class EmptyConfigPathValidator : IInstallationValidator
    {
        public Task<InstallationCandidate?> ValidateAsync(string executablePath, CancellationToken cancellationToken) =>
            Task.FromResult<InstallationCandidate?>(new InstallationCandidate(executablePath, "2.2.9.1", string.Empty));
    }
}
