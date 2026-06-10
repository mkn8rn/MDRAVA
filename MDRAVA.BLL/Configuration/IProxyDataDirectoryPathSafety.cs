namespace MDRAVA.BLL.Configuration;

public interface IProxyDataDirectoryPathSafety
{
    bool TryGetSafeRelativePath(string root, string path, out string relativePath);
}
