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

    [Fact]
    public async Task Logs_from_different_directories_do_not_overwrite_each_other()
    {
        Directory.CreateDirectory(_root);
        var first = Path.Combine(_root, "one", "wacs.log");
        var second = Path.Combine(_root, "two", "wacs.log");
        Directory.CreateDirectory(Path.GetDirectoryName(first)!);
        Directory.CreateDirectory(Path.GetDirectoryName(second)!);
        await File.WriteAllTextAsync(first, "first log");
        await File.WriteAllTextAsync(second, "second log");
        var destination = Path.Combine(_root, "diagnostic.zip");

        await new DiagnosticExporter([]).ExportAsync(destination, new Dictionary<string, string>(), [first, second], CancellationToken.None);

        using var archive = System.IO.Compression.ZipFile.OpenRead(destination);
        var logNames = archive.Entries.Select(x => x.Name)
            .Where(x => x.StartsWith("wacs", StringComparison.Ordinal) && x.EndsWith(".log", StringComparison.Ordinal))
            .ToArray();
        logNames.Should().HaveCount(2).And.OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Public_certificates_survive_export_but_private_keys_are_removed()
    {
        Directory.CreateDirectory(_root);
        var log = Path.Combine(_root, "output.log");
        await File.WriteAllTextAsync(log,
            "-----BEGIN CERTIFICATE-----\nMIIB\n-----END CERTIFICATE-----\n-----BEGIN PRIVATE KEY-----\nsecret\n-----END PRIVATE KEY-----\ndone");
        var destination = Path.Combine(_root, "diagnostic.zip");

        await new DiagnosticExporter([]).ExportAsync(destination, new Dictionary<string, string>(), [log], CancellationToken.None);

        using var archive = System.IO.Compression.ZipFile.OpenRead(destination);
        using var reader = new StreamReader(archive.Entries.Single(x => x.Name == "output.log").Open());
        var content = await reader.ReadToEndAsync();
        content.Should().Contain("BEGIN CERTIFICATE").And.Contain("[private key removed]").And.NotContain("secret");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
