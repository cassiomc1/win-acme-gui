using System.IO.Compression;

namespace WinAcmeGui.Infrastructure.Downloads;

public sealed class UnsafeArchiveException(string message) : Exception(message);

public sealed class SafeZipExtractor
{
    private const long MaxEntrySize = 512L * 1024 * 1024;

    public async Task ExtractAsync(string archivePath, string destination, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destination);
        var root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(archivePath);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(relative) || relative.Split(Path.DirectorySeparatorChar).Any(x => x == ".."))
                throw new UnsafeArchiveException($"Archive entry escapes destination: {entry.FullName}");
            var target = Path.GetFullPath(Path.Combine(destination, relative));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new UnsafeArchiveException($"Archive entry escapes destination: {entry.FullName}");
            if (!seen.Add(target)) throw new UnsafeArchiveException($"Duplicate archive entry: {entry.FullName}");
            if (entry.Length > MaxEntrySize) throw new UnsafeArchiveException($"Archive entry is too large: {entry.FullName}");
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(target);
                continue;
            }
            var parent = Path.GetDirectoryName(target)!;
            Directory.CreateDirectory(parent);
            if (File.Exists(target)) throw new UnsafeArchiveException($"Refusing to overwrite: {target}");
            await using var input = entry.Open();
            await using var output = File.Open(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await input.CopyToAsync(output, cancellationToken);
        }
    }
}
