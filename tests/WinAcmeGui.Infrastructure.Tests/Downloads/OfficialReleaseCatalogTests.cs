using System.Net;
using FluentAssertions;
using WinAcmeGui.Infrastructure.Downloads;

namespace WinAcmeGui.Infrastructure.Tests.Downloads;

public sealed class OfficialReleaseCatalogTests
{
    [Fact]
    public async Task Selects_x64_trimmed_asset_from_official_api()
    {
        var handler = new StubHandler("{\"tag_name\":\"v2.2.9.1\",\"assets\":[{\"name\":\"win-acme.v2.2.9.1.x64.trimmed.zip\",\"browser_download_url\":\"https://github.com/win-acme/win-acme/releases/download/v2.2.9.1/win-acme.v2.2.9.1.x64.trimmed.zip\",\"digest\":\"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}]}");
        var catalog = new OfficialReleaseCatalog(new HttpClient(handler), new PackageVerifier(["api.github.com", "github.com"]));

        var asset = await catalog.GetLatestAsync(CancellationToken.None);

        asset.Version.Should().Be("v2.2.9.1");
        asset.Architecture.Should().Be("x64");
        asset.DownloadUri.Host.Should().Be("github.com");
        asset.Sha256.Should().Be(new string('a', 64));
    }

    [Fact]
    public async Task Rejects_release_asset_without_sha256_digest()
    {
        var handler = new StubHandler("{\"tag_name\":\"v2.2.9.1\",\"assets\":[{\"name\":\"win-acme.v2.2.9.1.x64.trimmed.zip\",\"browser_download_url\":\"https://github.com/win-acme/win-acme/releases/download/v2.2.9.1/win-acme.v2.2.9.1.x64.trimmed.zip\"}]}");
        var catalog = new OfficialReleaseCatalog(new HttpClient(handler), new PackageVerifier(["api.github.com", "github.com"]));

        var act = () => catalog.GetLatestAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task Uses_the_pinned_digest_when_the_official_legacy_release_omits_one()
    {
        var handler = new StubHandler("{\"tag_name\":\"v2.2.9.1701\",\"assets\":[{\"name\":\"win-acme.v2.2.9.1701.x64.trimmed.zip\",\"browser_download_url\":\"https://github.com/win-acme/win-acme/releases/download/v2.2.9.1701/win-acme.v2.2.9.1701.x64.trimmed.zip\"}]}");
        var catalog = new OfficialReleaseCatalog(new HttpClient(handler), new PackageVerifier(["api.github.com", "github.com"]));

        var asset = await catalog.GetLatestAsync(CancellationToken.None);

        asset.Sha256.Should().Be("f4dc3b144841ffdba391ce168c273d7a686d45a359075e30ee4bf4ee186857d6");
    }

    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
    }
}
