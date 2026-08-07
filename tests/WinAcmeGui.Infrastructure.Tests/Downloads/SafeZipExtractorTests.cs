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
    public async Task Rejects_entries_outside_destination(string entryName)
    {
        Directory.CreateDirectory(_root);
        var archive = Path.Combine(_root, "bad.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(zip.CreateEntry(entryName).Open())) await writer.WriteAsync("payload");

        var act = () => new SafeZipExtractor().ExtractAsync(archive, Path.Combine(_root, "out"), CancellationToken.None);

        await act.Should().ThrowAsync<UnsafeArchiveException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
