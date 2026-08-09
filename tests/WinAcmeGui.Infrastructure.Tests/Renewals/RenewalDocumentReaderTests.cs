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
        result.Renewal.Should().NotBeNull();
        result.Renewal!.Status.Should().Be(WinAcmeGui.Domain.Renewals.RenewalStatus.Unreadable);
    }

    [Fact]
    public async Task Non_object_json_returns_diagnostic_instead_of_throwing()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "array.renewal.json");
        await File.WriteAllTextAsync(path, "[1,2,3]");

        var result = await new RenewalDocumentReader().ReadAsync(path, CancellationToken.None);

        result.IsReadable.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle(x => x.Code == "renewal.json.invalid");
    }

    [Fact]
    public async Task Expired_history_sets_expired_status()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "expired.renewal.json");
        await File.WriteAllTextAsync(path, "{\"Id\":\"expired\",\"Name\":\"expired.example.com\",\"Plugin\":{\"Source\":{\"Plugin\":\"Manual\",\"MainDomain\":\"expired.example.com\"}},\"History\":[{\"Success\":true,\"ValidTo\":\"2020-01-01T00:00:00Z\"}]}");

        var result = await new RenewalDocumentReader().ReadAsync(path, CancellationToken.None);

        result.Renewal!.Status.Should().Be(WinAcmeGui.Domain.Renewals.RenewalStatus.Expired);
    }

    [Fact]
    public async Task Incomplete_object_is_visible_but_read_only()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "incomplete.renewal.json");
        await File.WriteAllTextAsync(path, "{\"Id\":\"id-only\"}");

        var result = await new RenewalDocumentReader().ReadAsync(path, CancellationToken.None);

        result.Renewal!.Status.Should().Be(WinAcmeGui.Domain.Renewals.RenewalStatus.Unreadable);
        result.IsEditable.Should().BeFalse();
        result.Diagnostics.Should().Contain(x => x.Code == "renewal.json.incomplete");
    }

    [Fact]
    public async Task Uses_the_newest_history_entry_by_timestamp()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "history-order.renewal.json");
        await File.WriteAllTextAsync(path, "{\"Id\":\"id\",\"Name\":\"example.com\",\"Plugin\":{\"Source\":{\"Plugin\":\"Manual\",\"MainDomain\":\"example.com\"}},\"History\":[{\"Date\":\"2024-02-01T00:00:00Z\",\"Success\":true,\"ValidTo\":\"2099-01-01T00:00:00Z\"},{\"Date\":\"2024-03-01T00:00:00Z\",\"Success\":false,\"ValidTo\":\"2099-01-01T00:00:00Z\"}]}");

        var result = await new RenewalDocumentReader().ReadAsync(path, CancellationToken.None);

        result.Renewal!.Status.Should().Be(WinAcmeGui.Domain.Renewals.RenewalStatus.Failed);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
