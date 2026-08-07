namespace WinAcmeGui.Infrastructure.Downloads;

public sealed record ReleaseAsset(string Version, string Architecture, string Distribution, Uri DownloadUri, string? Sha256);

public sealed class OfficialReleaseClient(HttpClient httpClient, PackageVerifier verifier)
{
    public async Task<Stream> DownloadAsync(ReleaseAsset asset, CancellationToken cancellationToken)
    {
        if (!verifier.IsApproved(asset.DownloadUri)) throw new InvalidOperationException("Release source is not approved.");
        using var response = await httpClient.GetAsync(asset.DownloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        return new MemoryStream(await response.Content.ReadAsByteArrayAsync(cancellationToken), writable: false);
    }
}
