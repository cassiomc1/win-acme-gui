using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using FluentAssertions;
using WinAcmeGui.Infrastructure.Downloads;

namespace WinAcmeGui.Infrastructure.Tests.Downloads;

public sealed class WinAcmeDownloaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "win-acme-gui-downloader", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Verifies_hash_before_extracting_package()
    {
        Directory.CreateDirectory(_root);
        var package = CreateZip("wacs.exe", "binary");
        var hash = Convert.ToHexString(SHA256.HashData(package));
        var asset = new ReleaseAsset("v1", "x64", "trimmed", new Uri("https://github.com/win-acme/package.zip"), hash);
        var client = new OfficialReleaseClient(new HttpClient(new BytesHandler(package)), new PackageVerifier(["github.com"]));
        var destination = Path.Combine(_root, "out");

        await new WinAcmeDownloader(client, new SafeZipExtractor(), new AcceptingSignatureVerifier()).DownloadAndExtractAsync(asset, destination, null, CancellationToken.None);

        (await File.ReadAllTextAsync(Path.Combine(destination, "wacs.exe"))).Should().Be("binary");
    }

    [Fact]
    public async Task Official_package_integrity_check_does_not_require_Authenticode()
    {
        Directory.CreateDirectory(_root);
        var package = CreateZip("wacs.exe", "unsigned test binary");
        var hash = Convert.ToHexString(SHA256.HashData(package));
        var asset = new ReleaseAsset("v1", "x64", "pluggable", new Uri("https://github.com/win-acme/package.zip"), hash);
        var client = new OfficialReleaseClient(new HttpClient(new BytesHandler(package)), new PackageVerifier(["github.com"]));
        var destination = Path.Combine(_root, "out");

        await new WinAcmeDownloader(client, new SafeZipExtractor(), new PackageIntegrityVerifier())
            .DownloadAndExtractAsync(asset, destination, null, CancellationToken.None);

        File.Exists(Path.Combine(destination, "wacs.exe")).Should().BeTrue();
    }

    [Fact]
    public async Task Fails_closed_when_release_metadata_has_no_digest()
    {
        var asset = new ReleaseAsset("v1", "x64", "trimmed", new Uri("https://github.com/win-acme/package.zip"), null);
        var client = new OfficialReleaseClient(new HttpClient(new ThrowingHandler()), new PackageVerifier(["github.com"]));

        var act = () => new WinAcmeDownloader(client, new SafeZipExtractor(), new WindowsAuthenticodeSignatureVerifier()).DownloadAndExtractAsync(asset, Path.Combine(_root, "out"), null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task Does_not_publish_extracted_files_when_signature_verification_fails()
    {
        Directory.CreateDirectory(_root);
        var package = CreateZip("wacs.exe", "binary");
        var hash = Convert.ToHexString(SHA256.HashData(package));
        var asset = new ReleaseAsset("v1", "x64", "trimmed", new Uri("https://github.com/win-acme/package.zip"), hash);
        var client = new OfficialReleaseClient(new HttpClient(new BytesHandler(package)), new PackageVerifier(["github.com"]));
        var destination = Path.Combine(_root, "out");

        var act = () => new WinAcmeDownloader(client, new SafeZipExtractor(), new FailingSignatureVerifier())
            .DownloadAndExtractAsync(asset, destination, null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>();
        Directory.GetFiles(destination, "*", SearchOption.AllDirectories).Should().BeEmpty();
        Directory.GetDirectories(_root, "out.staging-*", SearchOption.TopDirectoryOnly).Should().BeEmpty();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private static byte[] CreateZip(string name, string content)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        using (var writer = new StreamWriter(zip.CreateEntry(name).Open()))
            writer.Write(content);
        return stream.ToArray();
    }

    private sealed class BytesHandler(byte[] content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new ByteArrayContent(content)
            });
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The network should not be called.");
    }

    private sealed class FailingSignatureVerifier : IPackageSignatureVerifier
    {
        public Task VerifyAsync(string destination, CancellationToken cancellationToken) =>
            throw new InvalidDataException("signature rejected");
    }

    private sealed class AcceptingSignatureVerifier : IPackageSignatureVerifier
    {
        public Task VerifyAsync(string destination, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
