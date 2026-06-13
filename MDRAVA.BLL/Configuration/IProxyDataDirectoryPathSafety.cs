namespace MDRAVA.BLL.Configuration;

public interface IProxyDataDirectoryPathSafety
{
    ProxySafeRelativePathResult GetSafeRelativePath(string root, string path);
}

public abstract record ProxySafeRelativePathResult
{
    private ProxySafeRelativePathResult()
    {
    }

    public static ProxySafeRelativePathResult Unsafe { get; } = new UnsafeResult();

    public static ProxySafeRelativePathResult Safe(string relativePath)
    {
        return new SafeResult(relativePath);
    }

    public sealed record SafeResult : ProxySafeRelativePathResult
    {
        public SafeResult(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new ArgumentException("Safe relative path is required.", nameof(relativePath));
            }

            RelativePath = relativePath;
        }

        public string RelativePath { get; }
    }

    public sealed record UnsafeResult : ProxySafeRelativePathResult;
}
