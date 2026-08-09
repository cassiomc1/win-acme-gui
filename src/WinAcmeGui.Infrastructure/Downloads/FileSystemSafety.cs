namespace WinAcmeGui.Infrastructure.Downloads;

internal static class FileSystemSafety
{
    public static void EnsureNoReparsePointsAlongPath(string path)
    {
        if (!OperatingSystem.IsWindows()) return;
        var current = Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(current))
        {
            try
            {
                if (File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
                    throw new UnsafeArchiveException($"Reparse points are not allowed in package paths: {current}");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }

            var parent = Path.GetDirectoryName(current);
            if (parent is null || parent.Equals(current, StringComparison.OrdinalIgnoreCase)) break;
            current = parent;
        }
    }
}
