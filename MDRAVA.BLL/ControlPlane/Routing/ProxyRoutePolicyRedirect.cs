namespace MDRAVA.BLL.ControlPlane.Routing;

public sealed record ProxyRoutePolicyRedirectInput(
    bool HttpsRedirectEnabled,
    int HttpsRedirectStatusCode,
    int? HttpsRedirectPort,
    bool CanonicalHostEnabled,
    string? CanonicalHostTargetHost,
    int CanonicalHostStatusCode,
    string ListenerTransport,
    string RequestHost,
    string RequestTarget);

public abstract record ProxyRoutePolicyRedirectDecision
{
    private ProxyRoutePolicyRedirectDecision()
    {
    }

    public static ProxyRoutePolicyRedirectDecision NoRedirect { get; } = new NoRedirectDecision();

    public static ProxyRoutePolicyRedirectDecision Redirect(int statusCode, string location)
    {
        return new RedirectDecision(statusCode, location);
    }

    public sealed record RedirectDecision : ProxyRoutePolicyRedirectDecision
    {
        public RedirectDecision(int statusCode, string location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentException("Policy redirect location is required.", nameof(location));
            }

            StatusCode = statusCode;
            Location = location;
        }

        public int StatusCode { get; }

        public string Location { get; }
    }

    private sealed record NoRedirectDecision : ProxyRoutePolicyRedirectDecision;
}

public static class ProxyRoutePolicyRedirectEvaluator
{
    public static ProxyRoutePolicyRedirectDecision Evaluate(ProxyRoutePolicyRedirectInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var scheme = string.Equals(input.ListenerTransport, "https", StringComparison.OrdinalIgnoreCase)
            ? "https"
            : "http";
        var host = input.RequestHost;
        var statusCode = 308;
        var shouldRedirect = false;

        if (input.HttpsRedirectEnabled
            && string.Equals(input.ListenerTransport, "http", StringComparison.OrdinalIgnoreCase))
        {
            scheme = "https";
            statusCode = input.HttpsRedirectStatusCode;
            shouldRedirect = true;
        }

        if (input.CanonicalHostEnabled
            && !string.IsNullOrWhiteSpace(input.CanonicalHostTargetHost)
            && !HostEquals(host, input.CanonicalHostTargetHost))
        {
            host = input.CanonicalHostTargetHost;
            statusCode = input.CanonicalHostStatusCode;
            shouldRedirect = true;
        }

        if (!shouldRedirect)
        {
            return ProxyRoutePolicyRedirectDecision.NoRedirect;
        }

        if (scheme == "https" && input.HttpsRedirectPort.HasValue)
        {
            host = ApplyPort(host, input.HttpsRedirectPort.Value, defaultPort: 443);
        }

        return ProxyRoutePolicyRedirectDecision.Redirect(
            statusCode,
            $"{scheme}://{host}{input.RequestTarget}");
    }

    private static bool HostEquals(string requestHost, string targetHost)
    {
        return string.Equals(StripSimplePort(requestHost), StripSimplePort(targetHost), StringComparison.OrdinalIgnoreCase);
    }

    private static string ApplyPort(string host, int port, int defaultPort)
    {
        var hostWithoutPort = StripSimplePort(host);
        return port == defaultPort
            ? hostWithoutPort
            : $"{hostWithoutPort}:{port}";
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
