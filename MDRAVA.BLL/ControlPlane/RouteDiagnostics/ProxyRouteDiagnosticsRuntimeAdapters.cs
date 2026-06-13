using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed class ProxyRouteDiagnosticsConfigurationSource
    : IProxyRouteDiagnosticsConfigurationSource
{
    private readonly IProxyConfigurationStore _configurationStore;

    public ProxyRouteDiagnosticsConfigurationSource(IProxyConfigurationStore configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public ProxyRouteDiagnosticsConfigurationReadResult Read()
    {
        if (!_configurationStore.TryGetSnapshot(out var runtimeSnapshot) || runtimeSnapshot is null)
        {
            return ProxyRouteDiagnosticsConfigurationReadResult.MissingConfiguration;
        }

        return ProxyRouteDiagnosticsConfigurationReadResult.Available(
            new ProxyRouteDiagnosticsRuntimeConfigurationSnapshot(runtimeSnapshot.Listeners, runtimeSnapshot.Routes));
    }
}

internal sealed class ProxyRouteDiagnosticsRuntimeConfigurationSnapshot
    : IProxyRouteDiagnosticsConfigurationSnapshot
{
    public ProxyRouteDiagnosticsRuntimeConfigurationSnapshot(
        IReadOnlyList<RuntimeListener> runtimeListeners,
        IReadOnlyList<RuntimeRoute> runtimeRoutes)
    {
        Listeners = runtimeListeners
            .Select(static listener => new ProxyRouteDiagnosticsRuntimeListener(listener))
            .ToArray();
        Routes = runtimeRoutes
            .Select(static route => new ProxyRouteDiagnosticsRuntimeRoute(route))
            .ToArray();
    }

    public IReadOnlyList<IProxyRouteDiagnosticsListener> Listeners { get; }

    public IReadOnlyList<IProxyRouteDiagnosticsRoute> Routes { get; }
}

internal sealed class ProxyRouteDiagnosticsRuntimeListener
    : IProxyRouteDiagnosticsListener
{
    public ProxyRouteDiagnosticsRuntimeListener(RuntimeListener runtimeListener)
    {
        Name = runtimeListener.Name;
        Transport = runtimeListener.Transport == RuntimeListenerTransport.Https ? "https" : "http";
        Address = runtimeListener.Address;
        Port = runtimeListener.Port;
        Enabled = runtimeListener.Enabled;
        Protocols = runtimeListener.Protocols;
        Http3EnabledForTraffic = runtimeListener.Http3.EnabledForTraffic;
    }

    public string Name { get; }

    public string Transport { get; }

    public string Address { get; }

    public int Port { get; }

    public bool Enabled { get; }

    public RuntimeListenerProtocols Protocols { get; }

    public bool Http3EnabledForTraffic { get; }
}

internal sealed class ProxyRouteDiagnosticsRuntimeRoute
    : IProxyRouteDiagnosticsRoute
{
    public ProxyRouteDiagnosticsRuntimeRoute(RuntimeRoute runtimeRoute)
    {
        SiteName = runtimeRoute.SiteName;
        Name = runtimeRoute.Name;
        Host = runtimeRoute.Host;
        PathPrefix = runtimeRoute.PathPrefix;
        Action = runtimeRoute.Action.ToString();
        MaintenanceEnabled = runtimeRoute.Maintenance.Enabled;
        Maintenance = new ProxyRouteDiagnosticsMaintenancePolicy(
            runtimeRoute.Maintenance.Enabled,
            runtimeRoute.Maintenance.RetryAfterSeconds,
            runtimeRoute.Maintenance.ContentType,
            runtimeRoute.Maintenance.Body);
        HttpsRedirect = new ProxyRouteDiagnosticsHttpsRedirectPolicy(
            runtimeRoute.HttpsRedirect.Enabled,
            runtimeRoute.HttpsRedirect.StatusCode,
            runtimeRoute.HttpsRedirect.HttpsPort);
        CanonicalHost = new ProxyRouteDiagnosticsCanonicalHostPolicy(
            runtimeRoute.CanonicalHost.Enabled,
            runtimeRoute.CanonicalHost.TargetHost,
            runtimeRoute.CanonicalHost.StatusCode);
        Redirect = new ProxyRouteDiagnosticsRedirectPolicy(
            runtimeRoute.Redirect.StatusCode,
            runtimeRoute.Redirect.TargetUrl,
            runtimeRoute.Redirect.TargetPath,
            runtimeRoute.Redirect.PreserveQuery);
        StaticResponse = new ProxyRouteDiagnosticsStaticResponse(
            runtimeRoute.StaticResponse.StatusCode,
            runtimeRoute.StaticResponse.ContentType,
            runtimeRoute.StaticResponse.Body);
        PathRewrite = new ProxyRouteDiagnosticsPathRewrite(
            runtimeRoute.PathRewrite.StripPrefix,
            runtimeRoute.PathRewrite.ReplacePrefix,
            runtimeRoute.PathRewrite.Replacement);
        MaxRequestBodyBytes = runtimeRoute.ResolvedOptions.MaxRequestBodyBytes;
        Upstreams = runtimeRoute.Upstreams
            .Select(static upstream => new ProxyRouteDiagnosticsRuntimeUpstream(upstream))
            .ToArray();
        CacheEnabled = runtimeRoute.Cache.Enabled;
        CacheMethods = runtimeRoute.Cache.Methods;
        RetryEnabled = runtimeRoute.Retry.Enabled;
        RetryMethods = runtimeRoute.Retry.RetryMethods;
    }

    public string SiteName { get; }

    public string Name { get; }

    public string Host { get; }

    public string PathPrefix { get; }

    public string Action { get; }

    public bool MaintenanceEnabled { get; }

    public ProxyRouteDiagnosticsMaintenancePolicy Maintenance { get; }

    public ProxyRouteDiagnosticsHttpsRedirectPolicy HttpsRedirect { get; }

    public ProxyRouteDiagnosticsCanonicalHostPolicy CanonicalHost { get; }

    public ProxyRouteDiagnosticsRedirectPolicy Redirect { get; }

    public ProxyRouteDiagnosticsStaticResponse StaticResponse { get; }

    public ProxyRouteDiagnosticsPathRewrite PathRewrite { get; }

    public long MaxRequestBodyBytes { get; }

    public IReadOnlyList<IProxyRouteDiagnosticsUpstream> Upstreams { get; }

    public bool CacheEnabled { get; }

    public IReadOnlyList<string> CacheMethods { get; }

    public bool RetryEnabled { get; }

    public IReadOnlyList<string> RetryMethods { get; }
}

internal sealed class ProxyRouteDiagnosticsRuntimeUpstream
    : IProxyRouteDiagnosticsUpstream
{
    public ProxyRouteDiagnosticsRuntimeUpstream(RuntimeUpstream runtimeUpstream)
    {
        Name = runtimeUpstream.Name;
        Scheme = runtimeUpstream.Scheme;
        Protocol = runtimeUpstream.Protocol;
        Endpoint = runtimeUpstream.Endpoint;
        Weight = runtimeUpstream.Weight;
        CircuitBreakerEnabled = runtimeUpstream.CircuitBreaker.Enabled;
    }

    public string Name { get; }

    public string Scheme { get; }

    public string Protocol { get; }

    public string Endpoint { get; }

    public int Weight { get; }

    public bool CircuitBreakerEnabled { get; }
}
