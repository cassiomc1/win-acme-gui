namespace WinAcmeGui.Infrastructure.Downloads;

public sealed class WinAcmeDownloader(
    OfficialReleaseClient client,
    SafeZipExtractor extractor,
    IPackageSignatureVerifier signatureVerifier) : IDisposable
{
    public void Dispose() => client.Dispose();

    public async Task<string> DownloadAndExtractAsync(ReleaseAsset asset, string destination, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (!PackageVerifier.IsSha256Digest(asset.Sha256))
            throw new InvalidDataException("Release metadata does not contain a valid SHA-256 digest.");
        var fullDestination = Path.GetFullPath(destination);
        FileSystemSafety.EnsureNoReparsePointsAlongPath(fullDestination);
        Directory.CreateDirectory(fullDestination);
        if (Directory.EnumerateFileSystemEntries(fullDestination).Any())
            throw new InvalidDataException("The package destination must be a new or empty directory.");
        var staging = fullDestination + ".staging-" + Guid.NewGuid().ToString("N");
        var archivePath = Path.Combine(Path.GetTempPath(), "win-acme-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            progress?.Report(0d);
            await client.DownloadToAsync(asset, archivePath, cancellationToken);
            await using var archive = File.OpenRead(archivePath);
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = await sha256.ComputeHashAsync(archive, cancellationToken);
            var hash = Convert.ToHexString(hashBytes);
            if (!hash.Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Downloaded package hash does not match release metadata.");
            await extractor.ExtractAsync(archivePath, staging, cancellationToken);
            await signatureVerifier.VerifyAsync(staging, cancellationToken);
            MergeStagingDirectory(staging, fullDestination);
            progress?.Report(1d);
            return fullDestination;
        }
        finally
        {
            try { if (File.Exists(archivePath)) File.Delete(archivePath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            try { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static void MergeStagingDirectory(string staging, string destination)
    {
        FileSystemSafety.EnsureNoReparsePointsAlongPath(destination);
        // Directories from the archive are merged too, so an archive that ships layout-only
        // folders keeps its shape in the destination.
        var directories = Directory.EnumerateDirectories(staging, "*", SearchOption.AllDirectories)
            .Select(path => Path.Combine(destination, Path.GetRelativePath(staging, path)))
            .ToArray();
        var files = Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories)
            .Select(path => new
            {
                Source = path,
                Target = Path.Combine(destination, Path.GetRelativePath(staging, path))
            })
            .ToArray();
        if (directories.Any(Directory.Exists) || files.Any(x => File.Exists(x.Target) || Directory.Exists(x.Target)))
            throw new InvalidDataException("Refusing to overwrite an existing package file.");

        var moved = new List<string>();
        var createdDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var directory in directories) EnsureDirectory(directory, createdDirectories);
            foreach (var file in files)
            {
                var targetDirectory = Path.GetDirectoryName(file.Target)!;
                FileSystemSafety.EnsureNoReparsePointsAlongPath(targetDirectory);
                EnsureDirectory(targetDirectory, createdDirectories);
                // overwrite:false makes the no-clobber promise atomic; the race between the
                // precheck above and this move surfaces as IOException and rolls back below.
                File.Move(file.Source, file.Target, overwrite: false);
                moved.Add(file.Target);
            }
        }
        catch
        {
            foreach (var file in moved.AsEnumerable().Reverse())
            {
                try { if (File.Exists(file)) File.Delete(file); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            foreach (var directory in createdDirectories.OrderByDescending(x => x.Length))
            {
                try
                {
                    if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            throw;
        }
    }

    private static void EnsureDirectory(string path, ISet<string> createdDirectories)
    {
        var missing = new Stack<string>();
        var current = Path.GetFullPath(path);
        while (!Directory.Exists(current))
        {
            missing.Push(current);
            var parent = Path.GetDirectoryName(current);
            if (parent is null || parent.Equals(current, StringComparison.OrdinalIgnoreCase)) break;
            current = parent;
        }
        while (missing.Count > 0)
        {
            var directory = missing.Pop();
            Directory.CreateDirectory(directory);
            createdDirectories.Add(directory);
        }
    }
}
