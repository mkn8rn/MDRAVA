namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed class ProxyRouteDiagnosticsMatcher
    : IProxyRouteDiagnosticsMatcher
{
    public IProxyRouteDiagnosticsRoute? Match(
        IReadOnlyList<IProxyRouteDiagnosticsRoute> routes,
        ProxyRouteDiagnosticsRequestHead requestHead)
    {
        foreach (var route in routes)
        {
            if (!HostMatches(route.Host, requestHead.Host))
            {
                continue;
            }

            if (!requestHead.Path.StartsWith(route.PathPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            return route;
        }

        return null;
    }

    private static bool HostMatches(string configuredHost, string requestHost)
    {
        if (configuredHost == "*")
        {
            return true;
        }

        if (string.Equals(configuredHost, requestHost, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var requestHostWithoutPort = StripSimplePort(requestHost);
        return string.Equals(configuredHost, requestHostWithoutPort, StringComparison.OrdinalIgnoreCase);
    }

    private static string StripSimplePort(string host)
    {
        var colonIndex = host.LastIndexOf(':');
        if (colonIndex <= 0 || host.Contains(']', StringComparison.Ordinal))
        {
            return host;
        }

        return host[..colonIndex];
    }
}
