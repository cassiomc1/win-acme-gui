using System.Net;
using FluentAssertions;
using WinAcmeGui.Infrastructure.Downloads;

namespace WinAcmeGui.Infrastructure.Tests.Downloads;

public sealed class OfficialReleaseCatalogTests
{
    [Fact]
    public async Task Selects_x64_trimmed_asset_from_official_api()
    {
        var handler = new StubHandler("{\"tag_name\":\"v2.2.9.1\",\"assets\":[{\"name\":\"win-acme.v2.2.9.1.x64.trimmed.zip\",\"browser_download_url\":\"https://github.com/win-acme/win-acme/releases/download/v2.2.9.1/win-acme.v2.2.9.1.x64.trimmed.zip\"}]}");
        var catalog = new OfficialReleaseCatalog(new HttpClient(handler), new PackageVerifier(["api.github.com", "github.com"]));

        var asset = await catalog.GetLatestAsync(CancellationToken.None);

        asset.Version.Should().Be("v2.2.9.1");
        asset.Architecture.Should().Be("x64");
        asset.DownloadUri.Host.Should().Be("github.com");
    }

    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
    }
}
