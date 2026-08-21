namespace WinAcmeGui.Infrastructure.Downloads;

public sealed record ReleaseAsset(string Version, string Architecture, string Distribution, Uri DownloadUri, string? Sha256);

public sealed class OfficialReleaseClient : IDisposable
{
    private const long MaxDownloadSize = 2L * 1024 * 1024 * 1024;
    private static readonly TimeSpan IdleReadTimeout = TimeSpan.FromSeconds(60);
    private readonly OfficialHttpTransport _transport;

    public OfficialReleaseClient(HttpClient httpClient, PackageVerifier verifier) => _transport = new(httpClient, verifier);

    public OfficialReleaseClient(PackageVerifier verifier) => _transport = new(verifier);

    public void Dispose() => _transport.Dispose();

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

            // A stalled transfer must not hang forever: the idle timer is re-armed before every
            // read and only a stall (not user cancellation) is surfaced as a timeout.
            using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = File.Open(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[128 * 1024];
                long total = 0;
                while (true)
                {
                    idleCts.CancelAfter(IdleReadTimeout);
                    var read = await input.ReadAsync(buffer.AsMemory(), idleCts.Token);
                    if (read == 0) break;
                    total += read;
                    if (total > MaxDownloadSize) throw new InvalidDataException("Downloaded package exceeds the maximum allowed size.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
                await output.FlushAsync(cancellationToken);
            }
            File.Move(partial, fullDestination);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The release download stalled and was aborted.");
        }
        finally
        {
            try { if (File.Exists(partial)) File.Delete(partial); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
