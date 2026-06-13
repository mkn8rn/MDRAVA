using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public interface IProxyRouteDiagnosticsConfigurationSource
{
    ProxyRouteDiagnosticsConfigurationReadResult Read();
}

public abstract record ProxyRouteDiagnosticsConfigurationReadResult
{
    private ProxyRouteDiagnosticsConfigurationReadResult()
    {
    }

    public static ProxyRouteDiagnosticsConfigurationReadResult MissingConfiguration { get; } =
        new MissingConfigurationResult();

    public static ProxyRouteDiagnosticsConfigurationReadResult Available(
        IProxyRouteDiagnosticsConfigurationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new AvailableResult(snapshot);
    }

    public sealed record AvailableResult : ProxyRouteDiagnosticsConfigurationReadResult
    {
        public AvailableResult(IProxyRouteDiagnosticsConfigurationSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            Snapshot = snapshot;
        }

        public IProxyRouteDiagnosticsConfigurationSnapshot Snapshot { get; }
    }

    public sealed record MissingConfigurationResult : ProxyRouteDiagnosticsConfigurationReadResult;
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

    RuntimeListenerProtocols Protocols { get; }

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

public sealed record ProxyRouteDiagnosticsActionDecision(
    bool ShouldProxy,
    int? GeneratedStatusCode)
{
    public static ProxyRouteDiagnosticsActionDecision Proxy { get; } = new(
        ShouldProxy: true,
        GeneratedStatusCode: null);

    public static ProxyRouteDiagnosticsActionDecision GeneratedResponse(int statusCode)
    {
        return new ProxyRouteDiagnosticsActionDecision(
            ShouldProxy: false,
            statusCode);
    }
}

public sealed record ProxyRouteDiagnosticsMaintenancePolicy(
    bool Enabled,
    int? RetryAfterSeconds,
    string ContentType,
    string Body);

public sealed record ProxyRouteDiagnosticsHttpsRedirectPolicy(
    bool Enabled,
    int StatusCode,
    int? HttpsPort);

public sealed record ProxyRouteDiagnosticsCanonicalHostPolicy(
    bool Enabled,
    string TargetHost,
    int StatusCode);

public sealed record ProxyRouteDiagnosticsRedirectPolicy(
    int StatusCode,
    string TargetUrl,
    string TargetPath,
    bool PreserveQuery);

public sealed record ProxyRouteDiagnosticsStaticResponse(
    int StatusCode,
    string ContentType,
    string Body);

public sealed record ProxyRouteDiagnosticsPathRewrite(
    string StripPrefix,
    string ReplacePrefix,
    string Replacement);
