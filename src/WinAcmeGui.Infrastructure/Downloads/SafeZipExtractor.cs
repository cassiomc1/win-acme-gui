using System.IO.Compression;

namespace WinAcmeGui.Infrastructure.Downloads;

public sealed class UnsafeArchiveException(string message) : Exception(message);

public sealed class SafeZipExtractor(
    long maxEntrySize = 512L * 1024 * 1024,
    long maxTotalSize = 2L * 1024 * 1024 * 1024)
{
    private readonly long _maxEntrySize = maxEntrySize > 0 ? maxEntrySize : throw new ArgumentOutOfRangeException(nameof(maxEntrySize));
    private readonly long _maxTotalSize = maxTotalSize > 0 ? maxTotalSize : throw new ArgumentOutOfRangeException(nameof(maxTotalSize));

    public async Task ExtractAsync(string archivePath, string destination, CancellationToken cancellationToken)
    {
        var fullDestination = Path.GetFullPath(destination);
        FileSystemSafety.EnsureNoReparsePointsAlongPath(fullDestination);
        Directory.CreateDirectory(fullDestination);
        FileSystemSafety.EnsureNoReparsePointsAlongPath(fullDestination);
        var root = fullDestination.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(archivePath);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<(ZipArchiveEntry Entry, string Target, bool IsDirectory)>();
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long totalSize = 0;

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.ExternalAttributes >> 16 is var mode && (mode & 0xF000) == 0xA000)
                throw new UnsafeArchiveException($"Symbolic links are not allowed: {entry.FullName}");

            var relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            if (string.IsNullOrEmpty(relative)) continue;
            if (relative.IndexOf('\0') >= 0
                || Path.IsPathRooted(relative)
                || LooksLikeWindowsAbsolutePath(relative)
                || relative.Split(Path.DirectorySeparatorChar).Any(x => x == ".."))
                throw new UnsafeArchiveException($"Archive entry escapes destination: {entry.FullName}");

            var target = Path.GetFullPath(Path.Combine(fullDestination, relative));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new UnsafeArchiveException($"Archive entry escapes destination: {entry.FullName}");
            FileSystemSafety.EnsureNoReparsePointsAlongPath(Path.GetDirectoryName(target)!);
            if (!seen.Add(target)) throw new UnsafeArchiveException($"Duplicate archive entry: {entry.FullName}");

            var isDirectory = entry.FullName.EndsWith("/", StringComparison.Ordinal) || string.IsNullOrEmpty(entry.Name);
            if (isDirectory)
            {
                if (File.Exists(target)) throw new UnsafeArchiveException($"Refusing to overwrite: {target}");
                directories.Add(target);
                entries.Add((entry, target, true));
                continue;
            }

            if (entry.Length < 0 || entry.Length > _maxEntrySize)
                throw new UnsafeArchiveException($"Archive entry is too large: {entry.FullName}");
            if (entry.Length > _maxTotalSize - totalSize)
                throw new UnsafeArchiveException("Archive exceeds the maximum uncompressed size.");
            totalSize += entry.Length;
            if (File.Exists(target) || Directory.Exists(target))
                throw new UnsafeArchiveException($"Refusing to overwrite: {target}");
            files.Add(target);
            entries.Add((entry, target, false));
        }

        foreach (var file in files)
        {
            var parent = Path.GetDirectoryName(file)!;
            if (files.Contains(parent) || directories.Contains(file))
                throw new UnsafeArchiveException($"Archive contains a file parent: {file}");
        }

        var createdFiles = new List<string>();
        var createdDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var (entry, target, isDirectory) in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (isDirectory)
                {
                    EnsureDirectory(target, createdDirectories);
                    continue;
                }

                var parent = Path.GetDirectoryName(target)!;
                EnsureDirectory(parent, createdDirectories);
                await using var input = entry.Open();
                await using var output = File.Open(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                createdFiles.Add(target);
                await input.CopyToAsync(output, cancellationToken);
            }
        }
        catch
        {
            Rollback(createdFiles, createdDirectories);
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

    private static void Rollback(IEnumerable<string> createdFiles, IEnumerable<string> createdDirectories)
    {
        foreach (var file in createdFiles.Reverse())
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
    }

    private static bool LooksLikeWindowsAbsolutePath(string path) =>
        path.Length >= 3
        && char.IsLetter(path[0])
        && path[1] == ':'
        && (path[2] == '\\' || path[2] == '/');
}
