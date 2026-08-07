using FluentAssertions;
using WinAcmeGui.Infrastructure.Configuration;

namespace WinAcmeGui.Infrastructure.Tests.Configuration;

public sealed class WinAcmeConfigurationReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "win-acme-gui-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Uses_configuration_path_override_without_mutating_source()
    {
        Directory.CreateDirectory(_root);
        var settingsPath = Path.Combine(_root, "settings.json");
        await File.WriteAllTextAsync(settingsPath, "{\"Client\":{\"ClientName\":\"custom\",\"ConfigurationPath\":\"D:\\\\AcmeConfig\"},\"ACME\":{\"DefaultBaseUri\":\"https://acme-staging-v02.api.letsencrypt.org/\"}}");
        var before = await File.ReadAllBytesAsync(settingsPath);
        var reader = new WinAcmeConfigurationReader(new StubVersionProbe("2.2.9.1"));

        var result = await reader.ReadAsync(Path.Combine(_root, "wacs.exe"), CancellationToken.None);

        result.ConfigurationPath.Should().Be(@"D:\AcmeConfig");
        (await File.ReadAllBytesAsync(settingsPath)).Should().Equal(before);
    }

    [Fact]
    public async Task Missing_settings_uses_program_data_default()
    {
        Directory.CreateDirectory(_root);
        var executable = Path.Combine(_root, "wacs.exe");
        await File.WriteAllTextAsync(executable, "");
        var reader = new WinAcmeConfigurationReader(new StubVersionProbe("2.2.9.1"));

        var result = await reader.ReadAsync(executable, CancellationToken.None);

        result.ClientName.Should().Be("win-acme");
        result.Endpoint.BaseUri.Should().Be(new Uri("https://acme-v02.api.letsencrypt.org/"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private sealed class StubVersionProbe(string version) : IWinAcmeVersionProbe
    {
        public Task<string> GetVersionAsync(string executablePath, CancellationToken cancellationToken) => Task.FromResult(version);
    }
}
