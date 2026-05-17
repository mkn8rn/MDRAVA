using MDRAVA.API.Proxy.Configuration;
using MDRAVA.API.Proxy.Acme;
using MDRAVA.API.Proxy.Caching;
using MDRAVA.API.Proxy.Configuration.Loading;
using MDRAVA.API.Proxy.Configuration.Paths;
using MDRAVA.API.Proxy.Configuration.Storage;
using MDRAVA.API.Proxy.Connections;
using MDRAVA.API.Proxy.Forwarding;
using MDRAVA.API.Proxy.Health;
using MDRAVA.API.Proxy.Http3;
using MDRAVA.API.Proxy.Metrics;
using MDRAVA.API.Proxy.Observability;
using MDRAVA.API.Proxy.Routing;
using MDRAVA.API.Proxy.Runtime;
using MDRAVA.API.Proxy.Resilience;
using MDRAVA.API.Proxy.Security;
using MDRAVA.API.Proxy.Tls;
using Microsoft.Extensions.Options;

namespace MDRAVA.API.Proxy.Hosting;

public static class ProxyServiceCollectionExtensions
{
    public static IServiceCollection AddProxyDataPlane(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<MdravaDataDirectoryOptions>()
            .Bind(configuration.GetSection(MdravaDataDirectoryOptions.SectionName));

        services.AddSingleton<IMdravaDataDirectoryProvider, MdravaDataDirectoryProvider>();
        services.AddSingleton<IValidateOptions<ProxyOptions>, ProxyOptionsValidator>();
        services.AddSingleton<ProxyDataDirectoryBootstrapper>();
        services.AddSingleton<SiteConfigurationParser>();
        services.AddSingleton<IProxyConfigurationNormalizer, ProxyConfigurationNormalizer>();
        services.AddSingleton<IProxyConfigurationStore, ProxyConfigurationStore>();
        services.AddSingleton<IProxyConfigurationLoader, ProxyConfigurationLoader>();
        services.AddSingleton<ProxyListenerService>();
        services.AddSingleton<IProxyListenerManager>(static services => services.GetRequiredService<ProxyListenerService>());
        services.AddSingleton<IProxyConfigurationReloadService, ProxyConfigurationReloadService>();
        services.AddSingleton<ProxyMetrics>();
        services.AddSingleton<PrometheusMetricsExporter>();
        services.AddSingleton<RequestIdGenerator>();
        services.AddSingleton<ResponseCacheStore>();
        services.AddSingleton<RecentRequestDiagnosticsStore>();
        services.AddSingleton<AccessLogEmitter>();
        services.AddSingleton<AdminAuditStore>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AcmeChallengeStore>();
        services.AddSingleton<AcmeHttp01ChallengeResponder>();
        services.AddSingleton<AcmeCertificateStatusStore>();
        services.AddSingleton<IAcmeCertificateIssuer, DisabledAcmeCertificateIssuer>();
        services.AddSingleton<AcmeCertificateManager>();
        services.AddSingleton<ProxyAdmissionController>();
        services.AddSingleton<CircuitBreakerStore>();
        services.AddSingleton<ProxyShutdownCoordinator>();
        services.AddSingleton<ClientRateLimiter>();
        services.AddSingleton<ProxyRuntimeState>();
        services.AddSingleton<UpstreamHealthStore>();
        services.AddSingleton<IRouteMatcher, SingleUpstreamRouteMatcher>();
        services.AddSingleton<IUpstreamSelector, RoundRobinUpstreamSelector>();
        services.AddSingleton<UpstreamConnectionFactory>();
        services.AddSingleton<UpstreamConnectionPool>();
        services.AddSingleton<UpstreamHealthCheckClient>();
        services.AddSingleton<HopByHopHeaderPolicy>();
        services.AddSingleton<ForwardedHeadersPolicy>();
        services.AddSingleton<UpgradeRequestPolicy>();
        services.AddSingleton<ProxyRouteActionPolicy>();
        services.AddSingleton<PathRewritePolicy>();
        services.AddSingleton<TunnelRelay>();
        services.AddSingleton<ProxyForwarder>();
        services.AddSingleton<UpgradeForwarder>();
        services.AddSingleton<TlsConnectionAuthenticator>();
        services.AddSingleton<IHttp3QuicListenerFactory, SystemHttp3QuicListenerFactory>();
        services.AddHostedService<ProxyConfigurationStartupService>();
        services.AddHostedService<AcmeRenewalService>();
        services.AddHostedService<UpstreamHealthCheckService>();
        services.AddHostedService(static services => services.GetRequiredService<ProxyListenerService>());

        return services;
    }
}
