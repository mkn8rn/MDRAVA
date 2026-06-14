namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public interface IProxyRouteDiagnosticsConfigurationSource
{
    ProxyRouteDiagnosticsConfigurationReadResult Read();
}

public interface IProxyRouteDiagnosticsConfigurationSnapshot
{
    IReadOnlyList<IProxyRouteDiagnosticsListener> Listeners { get; }

    IReadOnlyList<IProxyRouteDiagnosticsRoute> Routes { get; }
}

public interface IProxyRouteDiagnosticsMatcher
{
    IProxyRouteDiagnosticsRoute? Match(
        IReadOnlyList<IProxyRouteDiagnosticsRoute> routes,
        ProxyRouteDiagnosticsRequestHead requestHead);
}

public interface IProxyRouteDiagnosticsActionPolicy
{
    ProxyRouteDiagnosticsActionDecision Evaluate(
        IProxyRouteDiagnosticsRoute route,
        ProxyRouteDiagnosticsRequestHead requestHead,
        IProxyRouteDiagnosticsListener listener,
        bool isUpgradeRequest);
}

public interface IProxyRouteDiagnosticsPathRewritePolicy
{
    string Apply(IProxyRouteDiagnosticsRoute route, string target, string path);
}

public interface IProxyRouteDiagnosticsMetricsSink
{
    void RouteMatchDryRun(string? failureReason);
}

public interface IProxyRouteDiagnosticsListener
{
    string Name { get; }

    string Transport { get; }

    string Address { get; }

    int Port { get; }

    bool Enabled { get; }

    bool SupportsHttp1 { get; }

    bool SupportsHttp2 { get; }

    bool SupportsHttp3 { get; }

    bool Http3EnabledForTraffic { get; }
}

public interface IProxyRouteDiagnosticsRoute
{
    string SiteName { get; }

    string Name { get; }

    string Host { get; }

    string PathPrefix { get; }

    string Action { get; }

    bool MaintenanceEnabled { get; }

    ProxyRouteDiagnosticsMaintenancePolicy Maintenance { get; }

    ProxyRouteDiagnosticsHttpsRedirectPolicy HttpsRedirect { get; }

    ProxyRouteDiagnosticsCanonicalHostPolicy CanonicalHost { get; }

    ProxyRouteDiagnosticsRedirectPolicy Redirect { get; }

    ProxyRouteDiagnosticsStaticResponse StaticResponse { get; }

    ProxyRouteDiagnosticsPathRewrite PathRewrite { get; }

    long MaxRequestBodyBytes { get; }

    IReadOnlyList<IProxyRouteDiagnosticsUpstream> Upstreams { get; }

    bool CacheEnabled { get; }

    IReadOnlyList<string> CacheMethods { get; }

    bool RetryEnabled { get; }

    IReadOnlyList<string> RetryMethods { get; }
}

public interface IProxyRouteDiagnosticsUpstream
{
    string Name { get; }

    string Scheme { get; }

    string Protocol { get; }

    string Endpoint { get; }

    int Weight { get; }

    bool CircuitBreakerEnabled { get; }
}
