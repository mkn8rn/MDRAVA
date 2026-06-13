using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Backup;

public interface IProxyBackupFileSystem
{
    bool DirectoryExists(string root, string relativePath);

    ProxyBackupFileSystemScanResult ScanDataDirectory(string root);

    ProxySafeRelativePathResult GetSafeRelativePath(string root, string path);
}
