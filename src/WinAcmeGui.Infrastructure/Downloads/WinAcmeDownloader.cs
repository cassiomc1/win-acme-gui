using System.Security.Cryptography;

namespace WinAcmeGui.Infrastructure.Downloads;

public sealed class WinAcmeDownloader(OfficialReleaseClient client, SafeZipExtractor extractor)
{
    public async Task<string> DownloadAndExtractAsync(ReleaseAsset asset, string destination, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destination);
        var archivePath = Path.Combine(Path.GetTempPath(), "win-acme-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            await using (var input = await client.DownloadAsync(asset, cancellationToken))
            await using (var output = File.Open(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await input.CopyToAsync(output, cancellationToken);
            if (!string.IsNullOrWhiteSpace(asset.Sha256))
            {
                await using var archive = File.OpenRead(archivePath);
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(archive, cancellationToken));
                if (!hash.Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Downloaded package hash does not match release metadata.");
            }
            await extractor.ExtractAsync(archivePath, destination, cancellationToken);
            progress?.Report(1d);
            return destination;
        }
        finally
        {
            if (File.Exists(archivePath)) File.Delete(archivePath);
        }
    }
}
