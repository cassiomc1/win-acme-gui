using FluentAssertions;
using WinAcmeGui.Application.Configuration;
using WinAcmeGui.Domain.Installations;
using WinAcmeGui.Infrastructure.Configuration;
using WinAcmeGui.Infrastructure.Discovery;
using WinAcmeGui.Infrastructure.Downloads;

namespace WinAcmeGui.Infrastructure.Tests.Discovery;

public sealed class InstallationValidatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "win-acme-gui-validator", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Does_not_swallow_cancellation_from_version_probe()
    {
        Directory.CreateDirectory(_root);
        var executable = Path.Combine(_root, "wacs.exe");
        await File.WriteAllTextAsync(executable, string.Empty);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var validator = new InstallationValidator(new CancellingProbe());

        var act = () => validator.ValidateAsync(executable, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Returns_effective_configuration_path_and_endpoint()
    {
        Directory.CreateDirectory(_root);
        var executable = Path.Combine(_root, "wacs.exe");
        await File.WriteAllTextAsync(executable, string.Empty);
        var snapshot = new ConfigurationSnapshot(
            Path.Combine(_root, "settings.json"),
            "custom",
            Path.Combine(_root, "effective-config"),
            AcmeEndpoint.Staging,
            new Dictionary<string, string>());
        var validator = new InstallationValidator(new StubProbe(), new StubConfigurationReader(snapshot));

        var result = await validator.ValidateAsync(executable, CancellationToken.None);

        result.Should().NotBeNull();
        result!.ConfigurationPath.Should().Be(snapshot.ConfigurationPath);
        result.Endpoint.Should().Be(snapshot.Endpoint);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private sealed class CancellingProbe : IWinAcmeVersionProbe
    {
        public Task<string> GetVersionAsync(string executablePath, CancellationToken cancellationToken) =>
            Task.FromException<string>(new OperationCanceledException(cancellationToken));
    }

    private sealed class StubProbe : IWinAcmeVersionProbe
    {
        public Task<string> GetVersionAsync(string executablePath, CancellationToken cancellationToken) => Task.FromResult("2.2.9.1");
    }

    private sealed class StubConfigurationReader(ConfigurationSnapshot snapshot) : IWinAcmeConfigurationReader
    {
        public Task<ConfigurationSnapshot> ReadAsync(string executablePath, CancellationToken cancellationToken) => Task.FromResult(snapshot);
    }
}
