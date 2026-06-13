using MDRAVA.BLL.ControlPlane.Headers;
using MDRAVA.BLL.ControlPlane.HealthChecks;
using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.ControlPlane.Resilience;
using MDRAVA.BLL.ControlPlane.Routing;
using MDRAVA.BLL.ControlPlane.Upgrades;
using MDRAVA.BLL.ControlPlane.UpstreamSelection;
using MDRAVA.INF.Observability;
using MDRAVA.INF.Proxy.Connections;
using MDRAVA.INF.Proxy.Forwarding;
using MDRAVA.INF.Proxy.Health;
using MDRAVA.INF.Proxy.Http3;
using MDRAVA.INF.Proxy.Tls;

namespace MDRAVA.API.Proxy.Hosting;

public static partial class ProxyServiceCollectionExtensions
{
    private static void AddProxyForwardingServices(this IServiceCollection services)
    {
        services.AddSingleton<IRouteMatcher, SingleUpstreamRouteMatcher>();
        services.AddSingleton<IUpstreamSelector, RoundRobinUpstreamSelector>();
        services.AddSingleton<UpstreamConnectionFactory>();
        services.AddSingleton<UpstreamConnectionPool>();
        services.AddSingleton<IUpstreamConnectionPruner>(
            static services => services.GetRequiredService<UpstreamConnectionPool>());
        services.AddSingleton<Http3UpstreamConnectionPool>();
        services.AddSingleton<UpstreamHealthCheckClient>();
        services.AddSingleton<IUpstreamHealthCheckClient>(static services => services.GetRequiredService<UpstreamHealthCheckClient>());
        services.AddSingleton<IUpstreamHealthCheckEventSink, UpstreamHealthCheckLogger>();
        services.AddSingleton<IUpstreamHealthCheckTargetSource, ProxyConfigurationUpstreamHealthCheckTargetSource>();
        services.AddSingleton<UpstreamHealthCheckCoordinator>();
        services.AddSingleton<HopByHopHeaderPolicy>();
        services.AddSingleton<ForwardedHeadersPolicy>();
        services.AddSingleton<UpgradeRequestPolicy>();
        services.AddSingleton<ProxyRouteActionPolicy>();
        services.AddSingleton<PathRewritePolicy>();
        services.AddSingleton<Http3AltSvcPolicy>();
        services.AddSingleton<TunnelRelay>();
        services.AddSingleton<ProxyForwarder>();
        services.AddSingleton<UpgradeForwarder>();
        services.AddSingleton<TlsConnectionAuthenticator>();
        services.AddSingleton<IHttp3QuicListenerFactory, SystemHttp3QuicListenerFactory>();
    }
}
