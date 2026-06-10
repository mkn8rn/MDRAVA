using MDRAVA.BLL.Configuration;

namespace MDRAVA.INF.Configuration.Paths;

public sealed class ProxyRelativeStoragePathPolicy : IProxyRelativeStoragePathPolicy
{
    public bool IsSafeRelativePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || Path.IsPathRooted(value)
            || value.Contains("..", StringComparison.Ordinal)
            || ContainsControlCharacter(value))
        {
            return false;
        }

        return value.IndexOfAny(Path.GetInvalidPathChars()) < 0;
    }

    private static bool ContainsControlCharacter(string value)
    {
        return value.Any(char.IsControl);
    }
}
