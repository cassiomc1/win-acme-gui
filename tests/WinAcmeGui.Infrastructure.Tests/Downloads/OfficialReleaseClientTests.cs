using System.Net;
using FluentAssertions;
using WinAcmeGui.Infrastructure.Downloads;

namespace WinAcmeGui.Infrastructure.Tests.Downloads;

public sealed class OfficialReleaseClientTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "win-acme-gui-download", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Streams_package_to_destination_and_keeps_no_partial_file()
    {
        Directory.CreateDirectory(_root);
        var destination = Path.Combine(_root, "nested", "package.zip");
        var asset = new ReleaseAsset("v1", "x64", "trimmed", new Uri("https://github.com/win-acme/package.zip"), new string('a', 64));
        var client = new OfficialReleaseClient(
            new HttpClient(new BytesHandler("package content")),
            new PackageVerifier(["github.com"]));

        await client.DownloadToAsync(asset, destination, CancellationToken.None);

        (await File.ReadAllTextAsync(destination)).Should().Be("package content");
        Directory.GetFiles(Path.GetDirectoryName(destination)!, "*.part-*", SearchOption.TopDirectoryOnly).Should().BeEmpty();
    }

    [Fact]
    public async Task Rejects_a_redirected_response_from_an_unapproved_host()
    {
        Directory.CreateDirectory(_root);
        var destination = Path.Combine(_root, "package.zip");
        var asset = new ReleaseAsset("v1", "x64", "trimmed", new Uri("https://github.com/win-acme/package.zip"), new string('a', 64));
        var client = new OfficialReleaseClient(
            new HttpClient(new RedirectedResponseHandler()),
            new PackageVerifier(["github.com"]));

        var act = () => client.DownloadToAsync(asset, destination, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>();
        File.Exists(destination).Should().BeFalse();
    }

    [Fact]
    public async Task Follows_only_approved_github_asset_redirects()
    {
        Directory.CreateDirectory(_root);
        var destination = Path.Combine(_root, "package.zip");
        var asset = new ReleaseAsset("v1", "x64", "trimmed", new Uri("https://github.com/win-acme/package.zip"), new string('a', 64));
        var client = new OfficialReleaseClient(
            new HttpClient(new RedirectSequenceHandler()),
            new PackageVerifier(["github.com", "release-assets.githubusercontent.com"]));

        await client.DownloadToAsync(asset, destination, CancellationToken.None);

        (await File.ReadAllTextAsync(destination)).Should().Be("package content");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private sealed class BytesHandler(string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent(content)
            });
    }

    private sealed class RedirectedResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://evil.example/package.zip"),
                Content = new StringContent("should not be written")
            });
    }

    private sealed class RedirectSequenceHandler : HttpMessageHandler
    {
        private int _requests;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _requests++;
            if (_requests == 1)
            {
                var response = new HttpResponseMessage(HttpStatusCode.Found)
                {
                    Headers = { Location = new Uri("https://release-assets.githubusercontent.com/package.zip") },
                    RequestMessage = request
                };
                return Task.FromResult(response);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent("package content")
            });
        }
    }
}
