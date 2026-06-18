namespace MDRAVA.BLL.Configuration;

internal static class RuntimeRedirectFacts
{
    public static void ValidateHttpsRedirect(int statusCode, int? httpsPort)
    {
        ValidateRedirectStatusCode(statusCode);

        if (httpsPort is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(httpsPort));
        }
    }

    public static void ValidateCanonicalHost(
        bool enabled,
        string targetHost,
        int statusCode)
    {
        ValidateRedirectStatusCode(statusCode);
        ArgumentNullException.ThrowIfNull(targetHost);

        if (!enabled && string.IsNullOrWhiteSpace(targetHost))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(targetHost)
            || targetHost.Contains('/', StringComparison.Ordinal)
            || targetHost.Contains('\\', StringComparison.Ordinal)
            || targetHost.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                "Canonical host target must be a host name, not a URL or path.",
                nameof(targetHost));
        }
    }

    private static void ValidateRedirectStatusCode(int statusCode)
    {
        if (statusCode is not (301 or 302 or 303 or 307 or 308))
        {
            throw new ArgumentOutOfRangeException(nameof(statusCode));
        }
    }
}
