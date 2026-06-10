using MDRAVA.BLL.Configuration;

namespace MDRAVA.INF.Configuration;

public sealed class ProxyUrlSyntaxPolicy : IProxyUrlSyntaxPolicy
{
    public bool IsAbsoluteUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out _);
    }

    public bool IsAbsoluteHttpsUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }
}
