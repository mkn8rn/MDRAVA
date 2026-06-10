using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.Infrastructure;

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
    private readonly IRouteMatcher _routeMatcher;

    public ProxyRouteDiagnosticsMatcher(IRouteMatcher routeMatcher)
    {
        _routeMatcher = routeMatcher;
    }

    public IProxyRouteDiagnosticsRoute? Match(
        IProxyRouteDiagnosticsConfigurationSnapshot snapshot,
        ProxyRouteDiagnosticsRequestHead requestHead)
    {
        var runtimeSnapshot = RequireRuntimeSnapshot(snapshot);
        var match = _routeMatcher.Match(
            runtimeSnapshot.RuntimeSnapshot,
            ProxyRouteDiagnosticsRequestHeadMapper.ToHttp1RequestHead(requestHead));
        return match is null
            ? null
            : runtimeSnapshot.GetRoute(match.Route);
    }

    private static ProxyRouteDiagnosticsRuntimeConfigurationSnapshot RequireRuntimeSnapshot(
        IProxyRouteDiagnosticsConfigurationSnapshot snapshot)
    {
        return snapshot as ProxyRouteDiagnosticsRuntimeConfigurationSnapshot
            ?? throw new InvalidOperationException("Route diagnostics matcher requires the runtime configuration snapshot source.");
    }
}

public sealed class ProxyRouteDiagnosticsActionPolicyAdapter
    : IProxyRouteDiagnosticsActionPolicy
{
    private readonly ProxyRouteActionPolicy _routeActionPolicy;

    public ProxyRouteDiagnosticsActionPolicyAdapter(ProxyRouteActionPolicy routeActionPolicy)
    {
        _routeActionPolicy = routeActionPolicy;
    }

    public ProxyRouteDiagnosticsActionDecision Evaluate(
        IProxyRouteDiagnosticsRoute route,
        ProxyRouteDiagnosticsRequestHead requestHead,
        IProxyRouteDiagnosticsListener listener,
        bool isUpgradeRequest)
    {
        var runtimeRoute = RequireRuntimeRoute(route);
        var runtimeListener = RequireRuntimeListener(listener);
        var decision = _routeActionPolicy.Evaluate(
            runtimeRoute.RuntimeRoute,
            ProxyRouteDiagnosticsRequestHeadMapper.ToHttp1RequestHead(requestHead),
            runtimeListener.RuntimeListener,
            isUpgradeRequest);
        return new ProxyRouteDiagnosticsActionDecision(
            decision.ShouldProxy,
            decision.Response?.StatusCode);
    }

    private static ProxyRouteDiagnosticsRuntimeRoute RequireRuntimeRoute(
        IProxyRouteDiagnosticsRoute route)
    {
        return route as ProxyRouteDiagnosticsRuntimeRoute
            ?? throw new InvalidOperationException("Route diagnostics action policy requires a runtime route source.");
    }

    private static ProxyRouteDiagnosticsRuntimeListener RequireRuntimeListener(
        IProxyRouteDiagnosticsListener listener)
    {
        return listener as ProxyRouteDiagnosticsRuntimeListener
            ?? throw new InvalidOperationException("Route diagnostics action policy requires a runtime listener source.");
    }
}

public sealed class ProxyRouteDiagnosticsPathRewritePolicyAdapter
    : IProxyRouteDiagnosticsPathRewritePolicy
{
    private readonly PathRewritePolicy _pathRewritePolicy;

    public ProxyRouteDiagnosticsPathRewritePolicyAdapter(PathRewritePolicy pathRewritePolicy)
    {
        _pathRewritePolicy = pathRewritePolicy;
    }

    public string Apply(IProxyRouteDiagnosticsRoute route, string target, string path)
    {
        var runtimeRoute = route as ProxyRouteDiagnosticsRuntimeRoute
            ?? throw new InvalidOperationException("Route diagnostics path rewrite policy requires a runtime route source.");
        return _pathRewritePolicy.Apply(runtimeRoute.RuntimeRoute, target, path);
    }
}

internal sealed class ProxyRouteDiagnosticsRuntimeConfigurationSnapshot
    : IProxyRouteDiagnosticsConfigurationSnapshot
{
    private readonly IReadOnlyList<ProxyRouteDiagnosticsRuntimeRoute> _runtimeRoutes;

    public ProxyRouteDiagnosticsRuntimeConfigurationSnapshot(ProxyConfigurationSnapshot runtimeSnapshot)
    {
        RuntimeSnapshot = runtimeSnapshot;
        Listeners = runtimeSnapshot.Listeners
            .Select(static listener => new ProxyRouteDiagnosticsRuntimeListener(listener))
            .ToArray();
        var routes = runtimeSnapshot.Routes
            .Select(static route => new ProxyRouteDiagnosticsRuntimeRoute(route))
            .ToArray();
        _runtimeRoutes = routes;
        Routes = routes;
    }

    public ProxyConfigurationSnapshot RuntimeSnapshot { get; }

    public IReadOnlyList<IProxyRouteDiagnosticsListener> Listeners { get; }

    public IReadOnlyList<IProxyRouteDiagnosticsRoute> Routes { get; }

    public IProxyRouteDiagnosticsRoute GetRoute(RuntimeRoute route)
    {
        var wrappedRoute = _runtimeRoutes.FirstOrDefault(candidate => ReferenceEquals(candidate.RuntimeRoute, route));
        return wrappedRoute is not null
            ? wrappedRoute
            : new ProxyRouteDiagnosticsRuntimeRoute(route);
    }
}

internal sealed class ProxyRouteDiagnosticsRuntimeListener
    : IProxyRouteDiagnosticsListener
{
    public ProxyRouteDiagnosticsRuntimeListener(RuntimeListener runtimeListener)
    {
        RuntimeListener = runtimeListener;
    }

    public RuntimeListener RuntimeListener { get; }

    public string Name => RuntimeListener.Name;

    public string Transport => RuntimeListener.Transport == RuntimeListenerTransport.Https ? "https" : "http";

    public string Address => RuntimeListener.Address;

    public int Port => RuntimeListener.Port;

    public bool Enabled => RuntimeListener.Enabled;

    public RuntimeListenerProtocols Protocols => RuntimeListener.Protocols;

    public bool Http3EnabledForTraffic => RuntimeListener.Http3.EnabledForTraffic;
}

internal sealed class ProxyRouteDiagnosticsRuntimeRoute
    : IProxyRouteDiagnosticsRoute
{
    public ProxyRouteDiagnosticsRuntimeRoute(RuntimeRoute runtimeRoute)
    {
        RuntimeRoute = runtimeRoute;
        Upstreams = runtimeRoute.Upstreams
            .Select(static upstream => new ProxyRouteDiagnosticsRuntimeUpstream(upstream))
            .ToArray();
    }

    public RuntimeRoute RuntimeRoute { get; }

    public string SiteName => RuntimeRoute.SiteName;

    public string Name => RuntimeRoute.Name;

    public string Host => RuntimeRoute.Host;

    public string PathPrefix => RuntimeRoute.PathPrefix;

    public string Action => RuntimeRoute.Action.ToString();

    public bool MaintenanceEnabled => RuntimeRoute.Maintenance.Enabled;

    public long MaxRequestBodyBytes => RuntimeRoute.ResolvedOptions.MaxRequestBodyBytes;

    public IReadOnlyList<IProxyRouteDiagnosticsUpstream> Upstreams { get; }

    public bool CacheEnabled => RuntimeRoute.Cache.Enabled;

    public IReadOnlyList<string> CacheMethods => RuntimeRoute.Cache.Methods;

    public bool RetryEnabled => RuntimeRoute.Retry.Enabled;

    public IReadOnlyList<string> RetryMethods => RuntimeRoute.Retry.RetryMethods;
}

internal sealed class ProxyRouteDiagnosticsRuntimeUpstream
    : IProxyRouteDiagnosticsUpstream
{
    public ProxyRouteDiagnosticsRuntimeUpstream(RuntimeUpstream runtimeUpstream)
    {
        RuntimeUpstream = runtimeUpstream;
    }

    private RuntimeUpstream RuntimeUpstream { get; }

    public string Name => RuntimeUpstream.Name;

    public string Scheme => RuntimeUpstream.Scheme;

    public string Protocol => RuntimeUpstream.Protocol;

    public string Endpoint => RuntimeUpstream.Endpoint;

    public int Weight => RuntimeUpstream.Weight;

    public bool CircuitBreakerEnabled => RuntimeUpstream.CircuitBreaker.Enabled;
}

internal static class ProxyRouteDiagnosticsRequestHeadMapper
{
    public static Http1RequestHead ToHttp1RequestHead(ProxyRouteDiagnosticsRequestHead requestHead)
    {
        return new Http1RequestHead(
            requestHead.Method,
            requestHead.Target,
            requestHead.Path,
            requestHead.Version,
            requestHead.Host,
            ToHttp1RequestFraming(requestHead.Framing),
            requestHead.Headers
                .Select(static header => new Http1HeaderField(header.Name, header.Value))
                .ToArray());
    }

    private static Http1RequestFraming ToHttp1RequestFraming(ProxyRouteDiagnosticsRequestFraming framing)
    {
        return framing.Kind switch
        {
            ProxyRouteDiagnosticsBodyKind.ContentLength => Http1RequestFraming.FromContentLength(framing.ContentLength.GetValueOrDefault()),
            ProxyRouteDiagnosticsBodyKind.Chunked => Http1RequestFraming.Chunked,
            _ => Http1RequestFraming.None
        };
    }
}
