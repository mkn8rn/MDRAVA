namespace MDRAVA.BLL.ControlPlane.Backup;

public static class ProxyBackupPathSafety
{
    private const int MaxRelativePathLength = 240;

    public static bool TryGetSafeRelativePath(string root, string path, out string relativePath)
    {
        relativePath = "";
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(path);
        if (!IsInRoot(fullRoot, fullPath))
        {
            return false;
        }

        var relative = Path.GetRelativePath(fullRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/');
        if (relative.Length == 0
            || relative == "."
            || relative.StartsWith("..", StringComparison.Ordinal)
            || Path.IsPathRooted(relative))
        {
            return false;
        }

        relativePath = relative.Length <= MaxRelativePathLength
            ? relative
            : string.Concat(relative.AsSpan(0, MaxRelativePathLength - 12), "...truncated");
        return true;
    }

    private static bool IsInRoot(string root, string path)
    {
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    }
}
