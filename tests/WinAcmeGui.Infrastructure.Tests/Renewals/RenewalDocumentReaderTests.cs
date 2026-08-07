using FluentAssertions;
using WinAcmeGui.Infrastructure.Renewals;

namespace WinAcmeGui.Infrastructure.Tests.Renewals;

public sealed class RenewalDocumentReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "win-acme-gui-renewals", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Unknown_plugin_is_visible_but_not_editable()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "unknown.renewal.json");
        await File.WriteAllTextAsync(path, "{\"Id\":\"unknown\",\"Name\":\"unknown.example.com\",\"Plugin\":{\"Source\":{\"Plugin\":\"FutureSource\"}}}");

        var result = await new RenewalDocumentReader().ReadAsync(path, CancellationToken.None);

        result.IsReadable.Should().BeTrue();
        result.IsEditable.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle(x => x.Code == "renewal.plugin.unknown");
    }

    [Fact]
    public async Task Malformed_renewal_returns_diagnostic_instead_of_throwing()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "malformed.renewal.json");
        await File.WriteAllTextAsync(path, "{\"Id\":");

        var result = await new RenewalDocumentReader().ReadAsync(path, CancellationToken.None);

        result.IsReadable.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle(x => x.Code == "renewal.json.invalid");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
