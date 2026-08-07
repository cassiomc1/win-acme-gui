using FluentAssertions;
using WinAcmeGui.Infrastructure.Diagnostics;

namespace WinAcmeGui.Infrastructure.Tests.Diagnostics;

public sealed class DiagnosticExporterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "win-acme-gui-diagnostics", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Diagnostic_zip_contains_no_secret_or_private_key()
    {
        Directory.CreateDirectory(_root);
        var log = Path.Combine(_root, "renewal.log");
        await File.WriteAllTextAsync(log, "token=correct horse\nPUBLIC status=ok\n-----BEGIN PRIVATE KEY-----");
        var destination = Path.Combine(_root, "diagnostic.zip");
        var exporter = new DiagnosticExporter(new[] { "correct horse" });

        await exporter.ExportAsync(destination, new Dictionary<string, string> { ["status"] = "ok" }, [log], CancellationToken.None);

        using var archive = System.IO.Compression.ZipFile.OpenRead(destination);
        var text = string.Join('\n', archive.Entries.Select(entry =>
        {
            using var reader = new StreamReader(entry.Open());
            return reader.ReadToEnd();
        }));
        text.Should().NotContain("correct horse").And.NotContain("PRIVATE KEY").And.Contain("status");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
