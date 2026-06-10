namespace MDRAVA.BLL.Configuration;

public interface IProxyRelativeStoragePathPolicy
{
    bool IsSafeRelativePath(string value);
}
