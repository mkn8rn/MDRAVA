using MDRAVA.API.Proxy.Configuration;
using MDRAVA.API.Proxy.Acme;
using MDRAVA.API.Proxy.Caching;
using MDRAVA.API.Proxy.Configuration.Loading;
using MDRAVA.API.Proxy.Connections;
using MDRAVA.API.Proxy.Diagnostics;
using MDRAVA.API.Proxy.Forwarding;
using MDRAVA.API.Proxy.Health;
using MDRAVA.API.Proxy.Http3;
using MDRAVA.API.Proxy.Metrics;
using MDRAVA.API.Proxy.Observability;
using MDRAVA.API.Proxy.Runtime;
using MDRAVA.API.Proxy.Security;
using MDRAVA.API.Proxy.Tls;
using MDRAVA.BLL.Infrastructure;
using MDRAVA.INF.Configuration.Paths;
using MDRAVA.INF.DataDirectory;
using MDRAVA.INF.Observability;
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
        services.AddSingleton<IProxyConfigurationNormalizeSiteParser, ProxyConfigurationNormalizeSiteParser>();
        services.AddSingleton<ProxyConfigurationStore>();
        services.AddSingleton<IProxyConfigurationStore>(static services => services.GetRequiredService<ProxyConfigurationStore>());
        services.AddSingleton<IProxyActiveConfigurationVersionReader>(static services => services.GetRequiredService<ProxyConfigurationStore>());
        services.AddSingleton<ProxyConfigurationLoader>();
        services.AddSingleton<IProxyConfigurationLoader>(static services => services.GetRequiredService<ProxyConfigurationLoader>());
        services.AddSingleton<IProxyRestoreConfigurationValidator>(static services => services.GetRequiredService<ProxyConfigurationLoader>());
        services.AddSingleton<ProxyListenerReloadPlanner>();
        services.AddSingleton<ProxyListenerService>();
        services.AddSingleton<IProxyListenerManager>(static services => services.GetRequiredService<ProxyListenerService>());
        services.AddSingleton<ProxyConfigurationReloadService>();
        services.AddSingleton<IProxyConfigurationReloadOperations<ProxyConfigurationProjection>>(
            static services => services.GetRequiredService<ProxyConfigurationReloadService>());
        services.AddSingleton<IProxyConfigurationNormalizeOperations, ProxyConfigurationNormalizer>();
        services.AddSingleton<IProxyConfigurationValidationOperations>(static services => services.GetRequiredService<ProxyConfigurationReloadService>());
        services.AddSingleton<ProxyConfigurationAdministrationService>();
        services.AddSingleton<ProxyConfigurationReloadAdministrationService<ProxyConfigurationProjection>>();
        services.AddSingleton<IProxyConfigurationReadProjectionSource<ProxyConfigurationProjection>, ProxyConfigurationReadProjectionSource>();
        services.AddSingleton<IProxyConfigurationReadOperations<ProxyConfigurationProjection>, ProxyConfigurationReadOperations<ProxyConfigurationProjection>>();
        services.AddSingleton<ProxyConfigurationReadAdministrationService<ProxyConfigurationProjection>>();
        services.AddSingleton<ProxyMetrics>();
        services.AddSingleton<IProxyUpstreamSelectionMetricsSink>(
            static services => services.GetRequiredService<ProxyMetrics>());
        services.AddSingleton<IProxyCircuitBreakerMetricsSink>(
            static services => services.GetRequiredService<ProxyMetrics>());
        services.AddSingleton<IProxyUpstreamHealthMetricsSink>(
            static services => services.GetRequiredService<ProxyMetrics>());
        services.AddSingleton<IProxyHttp3AltSvcMetricsSink>(
            static services => services.GetRequiredService<ProxyMetrics>());
        services.AddSingleton<PrometheusMetricsExporter>();
        services.AddSingleton<IProxyMetricsExportAvailabilityReader, ProxyMetricsExportAvailabilityReader>();
        services.AddSingleton<ProxyMetricsExportAvailabilityService>();
        services.AddSingleton<IProxyMetricsExportProvider, ProxyMetricsExportProvider>();
        services.AddSingleton<ProxyMetricsAdministrationService>();
        services.AddSingleton<IProxyBackupFileSystem, ProxyBackupFileSystem>();
        services.AddSingleton<ProxyBackupService>();
        services.AddSingleton<IProxyBackupOperations>(static services => services.GetRequiredService<ProxyBackupService>());
        services.AddSingleton<ProxyBackupAdministrationService>();
        services.AddSingleton<IProxyConfigLintActiveConfigurationSource, ProxyConfigLintActiveConfigurationSource>();
        services.AddSingleton<IProxyConfigLintSubmittedConfigurationSource, ProxyConfigLintSubmittedConfigurationSource>();
        services.AddSingleton<IProxyConfigLintRuntimeStateSource, ProxyConfigLintRuntimeStateSource>();
        services.AddSingleton<IProxyConfigLintMetricsSink, ProxyConfigLintMetricsSink>();
        services.AddSingleton<ConfigLintService>();
        services.AddSingleton<IProxyConfigLintOperations>(static services => services.GetRequiredService<ConfigLintService>());
        services.AddSingleton<ProxyConfigLintAdministrationService>();
        services.AddSingleton<IProxyRouteDiagnosticsConfigurationSource, ProxyRouteDiagnosticsConfigurationSource>();
        services.AddSingleton<IProxyRouteDiagnosticsMatcher, ProxyRouteDiagnosticsMatcher>();
        services.AddSingleton<IProxyRouteDiagnosticsActionPolicy, ProxyRouteDiagnosticsActionPolicyAdapter>();
        services.AddSingleton<IProxyRouteDiagnosticsPathRewritePolicy, ProxyRouteDiagnosticsPathRewritePolicyAdapter>();
        services.AddSingleton<IProxyRouteDiagnosticsMetricsSink, ProxyRouteDiagnosticsMetricsSink>();
        services.AddSingleton<RouteMatchDiagnosticsService>();
        services.AddSingleton<IProxyRouteDiagnosticsOperations>(static services => services.GetRequiredService<RouteMatchDiagnosticsService>());
        services.AddSingleton<ProxyRouteDiagnosticsAdministrationService>();
        services.AddSingleton<RequestIdGenerator>();
        services.AddSingleton<ResponseCacheStore>();
        services.AddSingleton<IProxyCacheControl>(static services => services.GetRequiredService<ResponseCacheStore>());
        services.AddSingleton<IProxyCacheStatusConfigurationSource, ProxyCacheStatusConfigurationSource>();
        services.AddSingleton<IProxyCacheRuntimeStatusSource, ProxyCacheRuntimeStatusSource>();
        services.AddSingleton<ProxyCacheStatusReader>();
        services.AddSingleton<IProxyCacheStatusReader>(static services => services.GetRequiredService<ProxyCacheStatusReader>());
        services.AddSingleton<ProxyCacheAdministrationService>();
        services.AddSingleton<RecentRequestDiagnosticsStore>();
        services.AddSingleton<IProxyRequestDiagnosticsSource, ProxyRequestDiagnosticsSource>();
        services.AddSingleton<IProxyRequestDiagnosticsReader, ProxyRequestDiagnosticsReader>();
        services.AddSingleton<ProxyDiagnosticsAdministrationService>();
        services.AddSingleton<IProxyLogPersistenceSettingsReader, ProxyLogPersistenceSettingsReader>();
        services.AddSingleton<ProxyPersistentLogWriter>();
        services.AddSingleton<IProxyLogPersistenceStore>(static services => services.GetRequiredService<ProxyPersistentLogWriter>());
        services.AddSingleton<AccessLogEmitter>();
        services.AddSingleton<AdminAuditStore>();
        services.AddSingleton<IProxyAdminAuditReader, ProxyAdminAuditReader>();
        services.AddSingleton<ProxyAdminAuditAdministrationService>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AcmeChallengeStore>();
        services.AddSingleton<AcmeHttp01ChallengeResponder>();
        services.AddSingleton<AcmeCertificateStatusStore>();
        services.AddSingleton<IProxyAcmeStatusConfigurationSource, ProxyAcmeStatusConfigurationSource>();
        services.AddSingleton<IProxyAcmeCertificateLifecycleStatusSource, ProxyAcmeCertificateLifecycleStatusSource>();
        services.AddSingleton<IProxyAcmeStatusSnapshotReader, ProxyAcmeStatusSnapshotReader>();
        services.AddSingleton<ProxyAcmeAdministrationService>();
        services.AddSingleton<IAcmeCertificateIssuer, DisabledAcmeCertificateIssuer>();
        services.AddSingleton<AcmeCertificateManager>();
        services.AddSingleton<ProxyAdmissionController>();
        services.AddSingleton<IProxyRuntimeDirectoryProbe, ProxyRuntimeDirectoryProbe>();
        services.AddSingleton<ProxyRuntimePreflightService>();
        services.AddSingleton<IProxyStatusConfigurationSource>(static services => services.GetRequiredService<ProxyConfigurationStore>());
        services.AddSingleton<IProxyStatusMetricsSource>(static services => services.GetRequiredService<ProxyMetrics>());
        services.AddSingleton<IProxyStatusRuntimePreflightSource>(static services => services.GetRequiredService<ProxyRuntimePreflightService>());
        services.AddSingleton<IProxyStatusOperations, ProxyStatusOperations>();
        services.AddSingleton<ProxyStatusAdministrationService>();
        services.AddSingleton<CircuitBreakerStore>();
        services.AddSingleton<ProxyShutdownCoordinator>();
        services.AddSingleton<ClientRateLimiter>();
        services.AddSingleton<ProxyRuntimeState>();
        services.AddSingleton<IProxyStatusRuntimeStateSource>(static services => services.GetRequiredService<ProxyRuntimeState>());
        services.AddSingleton<UpstreamHealthStore>();
        services.AddSingleton<IProxyStatusUpstreamHealthSource>(static services => services.GetRequiredService<UpstreamHealthStore>());
        services.AddSingleton<IRouteMatcher, SingleUpstreamRouteMatcher>();
        services.AddSingleton<IUpstreamSelector, RoundRobinUpstreamSelector>();
        services.AddSingleton<UpstreamConnectionFactory>();
        services.AddSingleton<UpstreamConnectionPool>();
        services.AddSingleton<IUpstreamConnectionPruner>(
            static services => services.GetRequiredService<UpstreamConnectionPool>());
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
