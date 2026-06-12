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

    public bool TryRead(out IProxyRouteDiagnosticsConfigurationSnapshot? snapshot)
    {
        if (!_configurationStore.TryGetSnapshot(out var runtimeSnapshot) || runtimeSnapshot is null)
        {
            snapshot = null;
            return false;
        }

        snapshot = new ProxyRouteDiagnosticsRuntimeConfigurationSnapshot(runtimeSnapshot);
        return true;
    }
}

public sealed class ProxyRouteDiagnosticsMatcher
    : IProxyRouteDiagnosticsMatcher
{
    public IProxyRouteDiagnosticsRoute? Match(
        IProxyRouteDiagnosticsConfigurationSnapshot snapshot,
        ProxyRouteDiagnosticsRequestHead requestHead)
    {
        foreach (var route in snapshot.Routes)
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

public sealed class ProxyRouteDiagnosticsActionPolicyAdapter
    : IProxyRouteDiagnosticsActionPolicy
{
    public ProxyRouteDiagnosticsActionDecision Evaluate(
        IProxyRouteDiagnosticsRoute route,
        ProxyRouteDiagnosticsRequestHead requestHead,
        IProxyRouteDiagnosticsListener listener,
        bool isUpgradeRequest)
    {
        if (!isUpgradeRequest && TryBuildPolicyRedirect(route, requestHead, listener, out var policyRedirectStatusCode))
        {
            return new ProxyRouteDiagnosticsActionDecision(false, policyRedirectStatusCode);
        }

        if (route.Maintenance.Enabled)
        {
            return new ProxyRouteDiagnosticsActionDecision(false, 503);
        }

        if (string.Equals(route.Action, "Redirect", StringComparison.OrdinalIgnoreCase))
        {
            return new ProxyRouteDiagnosticsActionDecision(false, route.Redirect.StatusCode);
        }

        if (string.Equals(route.Action, "StaticResponse", StringComparison.OrdinalIgnoreCase))
        {
            return new ProxyRouteDiagnosticsActionDecision(false, route.StaticResponse.StatusCode);
        }

        return new ProxyRouteDiagnosticsActionDecision(true, null);
    }

    private static bool TryBuildPolicyRedirect(
        IProxyRouteDiagnosticsRoute route,
        ProxyRouteDiagnosticsRequestHead requestHead,
        IProxyRouteDiagnosticsListener listener,
        out int statusCode)
    {
        statusCode = 308;
        var shouldRedirect = false;

        if (route.HttpsRedirect.Enabled && string.Equals(listener.Transport, "http", StringComparison.OrdinalIgnoreCase))
        {
            statusCode = route.HttpsRedirect.StatusCode;
            shouldRedirect = true;
        }

        if (route.CanonicalHost.Enabled
            && !string.IsNullOrWhiteSpace(route.CanonicalHost.TargetHost)
            && !HostEquals(requestHead.Host, route.CanonicalHost.TargetHost))
        {
            statusCode = route.CanonicalHost.StatusCode;
            shouldRedirect = true;
        }

        return shouldRedirect;
    }

    private static bool HostEquals(string requestHost, string targetHost)
    {
        return string.Equals(StripSimplePort(requestHost), StripSimplePort(targetHost), StringComparison.OrdinalIgnoreCase);
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

public sealed class ProxyRouteDiagnosticsPathRewritePolicyAdapter
    : IProxyRouteDiagnosticsPathRewritePolicy
{
    public string Apply(IProxyRouteDiagnosticsRoute route, string target, string path)
    {
        var rewrite = route.PathRewrite;
        if (!string.IsNullOrWhiteSpace(rewrite.StripPrefix)
            && path.StartsWith(rewrite.StripPrefix, StringComparison.Ordinal))
        {
            return RewriteTarget(target, rewrite.StripPrefix, "");
        }

        if (!string.IsNullOrWhiteSpace(rewrite.ReplacePrefix)
            && path.StartsWith(rewrite.ReplacePrefix, StringComparison.Ordinal))
        {
            return RewriteTarget(target, rewrite.ReplacePrefix, rewrite.Replacement);
        }

        return target;
    }

    private static string RewriteTarget(string target, string oldPrefix, string newPrefix)
    {
        var queryIndex = target.IndexOf('?');
        var path = queryIndex < 0 ? target : target[..queryIndex];
        var query = queryIndex < 0 ? "" : target[queryIndex..];
        var remainder = path[oldPrefix.Length..];
        var rewrittenPath = string.IsNullOrEmpty(newPrefix) ? remainder : newPrefix + remainder;
        if (string.IsNullOrEmpty(rewrittenPath))
        {
            rewrittenPath = "/";
        }

        if (!rewrittenPath.StartsWith('/'))
        {
            rewrittenPath = "/" + rewrittenPath;
        }

        return rewrittenPath + query;
    }
}

internal sealed class ProxyRouteDiagnosticsRuntimeConfigurationSnapshot
    : IProxyRouteDiagnosticsConfigurationSnapshot
{
    public ProxyRouteDiagnosticsRuntimeConfigurationSnapshot(ProxyConfigurationSnapshot runtimeSnapshot)
    {
        Listeners = runtimeSnapshot.Listeners
            .Select(static listener => new ProxyRouteDiagnosticsRuntimeListener(listener))
            .ToArray();
        Routes = runtimeSnapshot.Routes
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
