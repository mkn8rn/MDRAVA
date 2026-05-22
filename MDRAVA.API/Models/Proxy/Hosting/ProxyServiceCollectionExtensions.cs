using MDRAVA.API.Proxy.Configuration;
using MDRAVA.API.Proxy.Acme;
using MDRAVA.API.Proxy.Backup;
using MDRAVA.API.Proxy.Caching;
using MDRAVA.API.Proxy.Configuration.Loading;
using MDRAVA.API.Proxy.Configuration.Storage;
using MDRAVA.API.Proxy.Connections;
using MDRAVA.API.Proxy.Diagnostics;
using MDRAVA.API.Proxy.Forwarding;
using MDRAVA.API.Proxy.Health;
using MDRAVA.API.Proxy.Http3;
using MDRAVA.API.Proxy.Metrics;
using MDRAVA.API.Proxy.Observability;
using MDRAVA.API.Proxy.Routing;
using MDRAVA.API.Proxy.Runtime;
using MDRAVA.API.Proxy.Resilience;
using MDRAVA.API.Proxy.Security;
using MDRAVA.API.Proxy.Status;
using MDRAVA.API.Proxy.Tls;
using MDRAVA.BLL.Infrastructure;
using MDRAVA.INF.Configuration.Paths;
using MDRAVA.INF.Runtime;
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

        services.AddSingleton<IMdravaDataDirectoryProvider>(static services =>
            new MdravaDataDirectoryProvider(
                services.GetRequiredService<IOptions<MdravaDataDirectoryOptions>>().Value));
        services.AddSingleton<IValidateOptions<ProxyOptions>, ProxyOptionsValidator>();
        services.AddSingleton<ProxyDataDirectoryBootstrapper>();
        services.AddSingleton<SiteConfigurationParser>();
        services.AddSingleton<IProxyConfigurationNormalizer, ProxyConfigurationNormalizer>();
        services.AddSingleton<IProxyConfigurationStore, ProxyConfigurationStore>();
        services.AddSingleton<IProxyConfigurationLoader, ProxyConfigurationLoader>();
        services.AddSingleton<ProxyListenerService>();
        services.AddSingleton<IProxyListenerManager>(static services => services.GetRequiredService<ProxyListenerService>());
        services.AddSingleton<IProxyConfigurationReloadService, ProxyConfigurationReloadService>();
        services.AddSingleton<IProxyConfigurationNormalizeOperations>(static services => services.GetRequiredService<IProxyConfigurationNormalizer>());
        services.AddSingleton<IProxyConfigurationValidationOperations>(static services => services.GetRequiredService<IProxyConfigurationReloadService>());
        services.AddSingleton<ProxyConfigurationAdministrationService>();
        services.AddSingleton<ProxyMetrics>();
        services.AddSingleton<PrometheusMetricsExporter>();
        services.AddSingleton<IProxyMetricsExportProvider, ProxyMetricsExportProvider>();
        services.AddSingleton<ProxyMetricsAdministrationService>();
        services.AddSingleton<ProxyBackupService>();
        services.AddSingleton<IProxyBackupOperations>(static services => services.GetRequiredService<ProxyBackupService>());
        services.AddSingleton<ProxyBackupAdministrationService>();
        services.AddSingleton<ConfigLintService>();
        services.AddSingleton<IProxyConfigLintOperations>(static services => services.GetRequiredService<ConfigLintService>());
        services.AddSingleton<ProxyConfigLintAdministrationService>();
        services.AddSingleton<RouteMatchDiagnosticsService>();
        services.AddSingleton<IProxyRouteDiagnosticsOperations>(static services => services.GetRequiredService<RouteMatchDiagnosticsService>());
        services.AddSingleton<ProxyRouteDiagnosticsAdministrationService>();
        services.AddSingleton<RequestIdGenerator>();
        services.AddSingleton<ResponseCacheStore>();
        services.AddSingleton<IProxyCacheControl>(static services => services.GetRequiredService<ResponseCacheStore>());
        services.AddSingleton<IProxyCacheStatusReader, ProxyCacheStatusReader>();
        services.AddSingleton<ProxyCacheAdministrationService>();
        services.AddSingleton<RecentRequestDiagnosticsStore>();
        services.AddSingleton<IProxyRequestDiagnosticsReader, ProxyRequestDiagnosticsReader>();
        services.AddSingleton<ProxyDiagnosticsAdministrationService>();
        services.AddSingleton<ProxyPersistentLogWriter>();
        services.AddSingleton<AccessLogEmitter>();
        services.AddSingleton<AdminAuditStore>();
        services.AddSingleton<IProxyAdminAuditReader, ProxyAdminAuditReader>();
        services.AddSingleton<ProxyAdminAuditAdministrationService>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AcmeChallengeStore>();
        services.AddSingleton<AcmeHttp01ChallengeResponder>();
        services.AddSingleton<AcmeCertificateStatusStore>();
        services.AddSingleton<IProxyAcmeStatusSnapshotReader, ProxyAcmeStatusSnapshotReader>();
        services.AddSingleton<ProxyAcmeAdministrationService>();
        services.AddSingleton<IAcmeCertificateIssuer, DisabledAcmeCertificateIssuer>();
        services.AddSingleton<AcmeCertificateManager>();
        services.AddSingleton<ProxyAdmissionController>();
        services.AddSingleton<IProxyRuntimeDirectoryProbe, ProxyRuntimeDirectoryProbe>();
        services.AddSingleton<ProxyRuntimePreflightService>();
        services.AddSingleton<IProxyStatusOperations, ProxyStatusOperations>();
        services.AddSingleton<ProxyStatusAdministrationService>();
        services.AddSingleton<CircuitBreakerStore>();
        services.AddSingleton<ProxyShutdownCoordinator>();
        services.AddSingleton<ClientRateLimiter>();
        services.AddSingleton<ProxyRuntimeState>();
        services.AddSingleton<UpstreamHealthStore>();
        services.AddSingleton<IRouteMatcher, SingleUpstreamRouteMatcher>();
        services.AddSingleton<IUpstreamSelector, RoundRobinUpstreamSelector>();
        services.AddSingleton<UpstreamConnectionFactory>();
        services.AddSingleton<UpstreamConnectionPool>();
        services.AddSingleton<Http3UpstreamConnectionPool>();
        services.AddSingleton<UpstreamHealthCheckClient>();
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
        services.AddHostedService<ProxyConfigurationStartupService>();
        services.AddHostedService<AcmeRenewalService>();
        services.AddHostedService<UpstreamHealthCheckService>();
        services.AddHostedService(static services => services.GetRequiredService<ProxyListenerService>());

        return services;
    }
}
