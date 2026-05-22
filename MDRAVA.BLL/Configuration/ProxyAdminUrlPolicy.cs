using System.Net;

namespace MDRAVA.BLL.Configuration;

public static class ProxyAdminUrlPolicy
{
    public static bool IsLocal(string url)
    {
        if (!TryCreateAbsoluteUri(url, out var uri))
        {
            return false;
        }

        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(uri.Host, "*", StringComparison.Ordinal)
            || string.Equals(uri.Host, "+", StringComparison.Ordinal))
        {
            return false;
        }

        return IPAddress.TryParse(uri.Host.Trim('[', ']'), out var address)
            && IPAddress.IsLoopback(address);
    }

    public static bool IsValid(string url)
    {
        return TryCreateAbsoluteUri(url, out _);
    }

    private static bool TryCreateAbsoluteUri(string url, out Uri uri)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out uri!))
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }
}
