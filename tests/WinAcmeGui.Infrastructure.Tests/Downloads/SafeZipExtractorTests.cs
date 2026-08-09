using System.IO.Compression;
using FluentAssertions;
using WinAcmeGui.Infrastructure.Downloads;

namespace WinAcmeGui.Infrastructure.Tests.Downloads;

public sealed class SafeZipExtractorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "win-acme-gui-zip", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("../escape.exe")]
    [InlineData("/absolute/wacs.exe")]
    [InlineData("C:\\absolute\\wacs.exe")]
    public async Task Rejects_entries_outside_destination(string entryName)
    {
        Directory.CreateDirectory(_root);
        var archive = Path.Combine(_root, "bad.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(zip.CreateEntry(entryName).Open())) await writer.WriteAsync("payload");

        var act = () => new SafeZipExtractor().ExtractAsync(archive, Path.Combine(_root, "out"), CancellationToken.None);

        await act.Should().ThrowAsync<UnsafeArchiveException>();
    }

    [Fact]
    public async Task Does_not_leave_partial_files_when_a_later_entry_conflicts()
    {
        Directory.CreateDirectory(_root);
        var archive = Path.Combine(_root, "partial.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            await using (var first = new StreamWriter(zip.CreateEntry("new.txt").Open()))
                await first.WriteAsync("new content");
            await using (var second = new StreamWriter(zip.CreateEntry("existing.txt").Open()))
                await second.WriteAsync("replacement");
        }

        var destination = Path.Combine(_root, "out");
        Directory.CreateDirectory(destination);
        await File.WriteAllTextAsync(Path.Combine(destination, "existing.txt"), "original");

        var act = () => new SafeZipExtractor().ExtractAsync(archive, destination, CancellationToken.None);

        await act.Should().ThrowAsync<UnsafeArchiveException>();
        File.Exists(Path.Combine(destination, "new.txt")).Should().BeFalse();
        (await File.ReadAllTextAsync(Path.Combine(destination, "existing.txt"))).Should().Be("original");
    }

    [Fact]
    public async Task Rejects_archive_that_exceeds_total_uncompressed_limit_before_writing()
    {
        Directory.CreateDirectory(_root);
        var archive = Path.Combine(_root, "large.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            await using (var first = new StreamWriter(zip.CreateEntry("one.txt").Open()))
                await first.WriteAsync(new string('a', 80));
            await using (var second = new StreamWriter(zip.CreateEntry("two.txt").Open()))
                await second.WriteAsync(new string('b', 80));
        }

        var destination = Path.Combine(_root, "out");
        var act = () => new SafeZipExtractor(maxEntrySize: 100, maxTotalSize: 150).ExtractAsync(archive, destination, CancellationToken.None);

        await act.Should().ThrowAsync<UnsafeArchiveException>();
        Directory.Exists(destination).Should().BeTrue();
        Directory.GetFiles(destination).Should().BeEmpty();
    }

    [Fact]
    public async Task Rejects_symbolic_link_entries()
    {
        Directory.CreateDirectory(_root);
        var archive = Path.Combine(_root, "link.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("link");
            entry.ExternalAttributes = unchecked((int)(0xA000u << 16));
            await using var writer = new StreamWriter(entry.Open());
            await writer.WriteAsync("target");
        }

        var act = () => new SafeZipExtractor().ExtractAsync(archive, Path.Combine(_root, "out"), CancellationToken.None);

        await act.Should().ThrowAsync<UnsafeArchiveException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
