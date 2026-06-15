using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed class ProxyRouteDiagnosticsRuntimeRoute
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
        CacheMethods = runtimeRoute.Cache.Methods.ToArray();
        RetryEnabled = runtimeRoute.Retry.Enabled;
        RetryMethods = runtimeRoute.Retry.RetryMethods.ToArray();
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
