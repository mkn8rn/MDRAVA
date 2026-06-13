using MDRAVA.BLL.Http;

namespace MDRAVA.BLL.Configuration;

public static partial class ProxyOptionsValidationRules
{
    private static bool IsRouteAction(string action)
    {
        return IsProxyAction(action) || IsRedirectAction(action) || IsStaticResponseAction(action);
    }

    private static bool IsProxyAction(string action)
    {
        return string.Equals(action, "proxy", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRedirectAction(string action)
    {
        return string.Equals(action, "redirect", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStaticResponseAction(string action)
    {
        return string.Equals(action, "staticResponse", StringComparison.OrdinalIgnoreCase);
    }

    private static string SupportedListenerProtocolsText()
    {
        return string.Join(
            ", ",
            RuntimeListenerProtocolExtensions.SupportedConfigValues.Select(static value => $"'{value}'"));
    }

    private static string SupportedHttp3EnablementsText()
    {
        return string.Join(
            ", ",
            RuntimeHttp3Compatibility.SupportedEnablementConfigValues.Select(static value => $"'{value}'"));
    }

    private static bool IsSupportedUpstreamScheme(string scheme)
    {
        return string.Equals(scheme, "http", StringComparison.OrdinalIgnoreCase)
            || string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSupportedUpstreamProtocol(string protocol)
    {
        return string.Equals(protocol, RuntimeUpstreamProtocol.Http1, StringComparison.OrdinalIgnoreCase)
            || string.Equals(protocol, RuntimeUpstreamProtocol.Http2, StringComparison.OrdinalIgnoreCase)
            || string.Equals(protocol, RuntimeUpstreamProtocol.Http3, StringComparison.OrdinalIgnoreCase);
    }
}
