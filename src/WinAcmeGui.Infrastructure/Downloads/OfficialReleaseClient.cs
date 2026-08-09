namespace WinAcmeGui.Infrastructure.Downloads;

public sealed record ReleaseAsset(string Version, string Architecture, string Distribution, Uri DownloadUri, string? Sha256);

public sealed class OfficialReleaseClient
{
    private const long MaxDownloadSize = 2L * 1024 * 1024 * 1024;
    private readonly OfficialHttpTransport _transport;

    public OfficialReleaseClient(HttpClient httpClient, PackageVerifier verifier) => _transport = new(httpClient, verifier);

    public OfficialReleaseClient(PackageVerifier verifier) => _transport = new(verifier);

    public async Task DownloadToAsync(ReleaseAsset asset, string destination, CancellationToken cancellationToken)
    {
        var fullDestination = Path.GetFullPath(destination);
        var parent = Path.GetDirectoryName(fullDestination);
        if (parent is null) throw new InvalidDataException("Download destination has no parent directory.");
        Directory.CreateDirectory(parent);
        var partial = fullDestination + ".part-" + Guid.NewGuid().ToString("N");
        try
        {
            using var response = await _transport.GetAsync(asset.DownloadUri, cancellationToken);
            if (response.Content.Headers.ContentLength is > MaxDownloadSize)
                throw new InvalidDataException("Downloaded package exceeds the maximum allowed size.");

            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = File.Open(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[128 * 1024];
                long total = 0;
                while (true)
                {
                    var read = await input.ReadAsync(buffer.AsMemory(), cancellationToken);
                    if (read == 0) break;
                    total += read;
                    if (total > MaxDownloadSize) throw new InvalidDataException("Downloaded package exceeds the maximum allowed size.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
                await output.FlushAsync(cancellationToken);
            }
            File.Move(partial, fullDestination);
        }
        finally
        {
            try { if (File.Exists(partial)) File.Delete(partial); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
