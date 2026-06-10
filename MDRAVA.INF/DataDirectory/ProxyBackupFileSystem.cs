using MDRAVA.BLL.ControlPlane;
using MDRAVA.BLL.ControlPlane.Backup;
using MDRAVA.BLL.Infrastructure;

namespace MDRAVA.INF.DataDirectory;

public sealed class ProxyBackupFileSystem : IProxyBackupFileSystem
{
    public bool DirectoryExists(string root, string relativePath)
    {
        return Directory.Exists(ResolveRelativePath(root, relativePath));
    }

    public ProxyBackupFileSystemScanResult ScanDataDirectory(string root)
    {
        if (!Directory.Exists(root))
        {
            return new ProxyBackupFileSystemScanResult(false, [], []);
        }

        List<ProxyBackupFileSystemEntry> files = [];
        List<ProxyBackupFileSystemWarning> warnings = [];
        ScanDirectory(root, root, files, warnings);
        return new ProxyBackupFileSystemScanResult(true, files, warnings);
    }

    public bool TryGetSafeRelativePath(string root, string path, out string relativePath)
    {
        return ProxyBackupPathSafety.TryGetSafeRelativePath(root, path, out relativePath);
    }

    private static void ScanDirectory(
        string root,
        string directory,
        List<ProxyBackupFileSystemEntry> files,
        List<ProxyBackupFileSystemWarning> warnings)
    {
        DirectoryInfo directoryInfo;
        try
        {
            directoryInfo = new DirectoryInfo(directory);
        }
        catch
        {
            warnings.Add(new ProxyBackupFileSystemWarning(
                "directory_unreadable",
                SafeRelativeOrNull(root, directory)));
            return;
        }

        if (directoryInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            warnings.Add(new ProxyBackupFileSystemWarning(
                "reparse_point_skipped",
                SafeRelativeOrNull(root, directory)));
            return;
        }

        FileInfo[] directoryFiles;
        try
        {
            directoryFiles = directoryInfo.GetFiles();
        }
        catch
        {
            warnings.Add(new ProxyBackupFileSystemWarning(
                "directory_unreadable",
                SafeRelativeOrNull(root, directory)));
            return;
        }

        foreach (var file in directoryFiles)
        {
            if (file.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                warnings.Add(new ProxyBackupFileSystemWarning(
                    "reparse_point_skipped",
                    SafeRelativeOrNull(root, file.FullName)));
                continue;
            }

            if (!ProxyBackupPathSafety.TryGetSafeRelativePath(root, file.FullName, out var relativePath))
            {
                warnings.Add(new ProxyBackupFileSystemWarning(
                    "unsafe_path_skipped",
                    null));
                continue;
            }

            files.Add(new ProxyBackupFileSystemEntry(
                relativePath,
                file.Length,
                file.LastWriteTimeUtc));
        }

        DirectoryInfo[] children;
        try
        {
            children = directoryInfo.GetDirectories();
        }
        catch
        {
            warnings.Add(new ProxyBackupFileSystemWarning(
                "directory_unreadable",
                SafeRelativeOrNull(root, directory)));
            return;
        }

        foreach (var child in children)
        {
            ScanDirectory(root, child.FullName, files, warnings);
        }
    }

    private static string ResolveRelativePath(string root, string relativePath)
    {
        if (string.Equals(relativePath, ".", StringComparison.Ordinal))
        {
            return root;
        }

        return Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string? SafeRelativeOrNull(string root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return ProxyBackupPathSafety.TryGetSafeRelativePath(root, path, out var relativePath)
            ? relativePath
            : null;
    }
}
