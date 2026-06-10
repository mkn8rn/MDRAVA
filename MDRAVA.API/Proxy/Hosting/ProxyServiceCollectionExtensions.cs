using MDRAVA.BLL.ControlPlane.Headers;
using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.ControlPlane.Listeners;
using MDRAVA.BLL.ControlPlane.Routing;
using MDRAVA.BLL.ControlPlane.RuntimeGuards;
using MDRAVA.BLL.ControlPlane.UpstreamSelection;
using MDRAVA.BLL.ControlPlane.Upgrades;
using MDRAVA.BLL.ControlPlane.Resilience;
using MDRAVA.BLL.ControlPlane.HealthChecks;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;
using MDRAVA.BLL.ControlPlane.Status;
using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Metrics;
using MDRAVA.BLL.ControlPlane.Observability;
using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.AdminAudit;
using MDRAVA.BLL.ControlPlane.AdminAuthentication;
using MDRAVA.BLL.ControlPlane.Backup;
using MDRAVA.BLL.ControlPlane.Caching;
using MDRAVA.BLL.ControlPlane.ConfigLint;
using MDRAVA.BLL.ControlPlane.RequestDiagnostics;
using MDRAVA.BLL.ControlPlane.RouteDiagnostics;
using MDRAVA.BLL.ControlPlane.RuntimePreflight;
using MDRAVA.API.Proxy.Security;
using MDRAVA.INF.Acme;
using MDRAVA.INF.Configuration;
using MDRAVA.INF.Configuration.Loading;
using MDRAVA.INF.Configuration.Paths;
using MDRAVA.INF.DataDirectory;
using MDRAVA.INF.Observability;
using MDRAVA.INF.Proxy.Connections;
using MDRAVA.INF.Proxy.Forwarding;
using MDRAVA.INF.Proxy.Health;
using MDRAVA.INF.Proxy.Hosting;
using MDRAVA.INF.Proxy.Http3;
using MDRAVA.INF.Proxy.RuntimeGuards;
using MDRAVA.INF.Proxy.Tls;
using MDRAVA.INF.Runtime;
using Microsoft.Extensions.Options;

namespace MDRAVA.API.Proxy.Hosting;

public static class ProxyServiceCollectionExtensions
{
    public static IServiceCollection AddProxyDataPlane(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddProxyConfigurationServices(configuration);
        services.AddProxyMetricsAndLoggingServices();
        services.AddProxyAdministrationServices();
        services.AddProxyAcmeServices();
        services.AddProxyRuntimeServices();
        services.AddProxyForwardingServices();
        services.AddProxyHostedServices();

        return services;
    }

    private static void AddProxyConfigurationServices(
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
        services.AddSingleton<IProxyConfigurationReloadEventSink, ProxyConfigurationReloadLogger>();
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
        services.AddSingleton<IProxyAdminUrlPolicy, ProxyAdminUrlPolicy>();
        services.AddSingleton<IProxyEndpointAddressPolicy, ProxyEndpointAddressPolicy>();
        services.AddSingleton<IProxyRelativeStoragePathPolicy, ProxyRelativeStoragePathPolicy>();
        services.AddSingleton<IProxyUrlSyntaxPolicy, ProxyUrlSyntaxPolicy>();
        services.AddSingleton<ProxyForwardedHeadersAddressPolicy>();
        services.AddSingleton<IProxyTrustedProxyPolicy>(static services => services.GetRequiredService<ProxyForwardedHeadersAddressPolicy>());
        services.AddSingleton<IForwardedHeadersAddressPolicy>(static services => services.GetRequiredService<ProxyForwardedHeadersAddressPolicy>());
        services.AddSingleton<IProxyDataDirectoryPathSafety, ProxyDataDirectoryPathSafety>();
    }

    private static void AddProxyMetricsAndLoggingServices(this IServiceCollection services)
    {
        services.AddSingleton<ProxyMetrics>();
        services.AddSingleton<IProxyUpstreamSelectionMetricsSink>(
            static services => services.GetRequiredService<ProxyMetrics>());
        services.AddSingleton<IProxyCircuitBreakerMetricsSink>(
            static services => services.GetRequiredService<ProxyMetrics>());
        services.AddSingleton<IProxyUpstreamHealthMetricsSink>(
            static services => services.GetRequiredService<ProxyMetrics>());
        services.AddSingleton<IProxyHttp3AltSvcMetricsSink>(
            static services => services.GetRequiredService<ProxyMetrics>());
        services.AddSingleton<IProxyAccessLogMetricsSink>(
            static services => services.GetRequiredService<ProxyMetrics>());
        services.AddSingleton<IProxyRequestDiagnosticsMetricsSink>(
            static services => services.GetRequiredService<ProxyMetrics>());
        services.AddSingleton<IProxyRequestIdMetricsSink>(
            static services => services.GetRequiredService<ProxyMetrics>());
        services.AddSingleton<IProxyRateLimitMetricsSink>(
            static services => services.GetRequiredService<ProxyMetrics>());
        services.AddSingleton<IProxyAdmissionMetricsSink>(
            static services => services.GetRequiredService<ProxyMetrics>());
        services.AddSingleton<IProxyConfigurationReloadMetricsSink>(
            static services => services.GetRequiredService<ProxyMetrics>());
        services.AddSingleton<IProxyHealthCheckMetricsSink>(
            static services => services.GetRequiredService<ProxyMetrics>());
        services.AddSingleton<IProxyAcmeMetricsSink>(
            static services => services.GetRequiredService<ProxyMetrics>());
        services.AddSingleton<IProxyAdminAuthenticationMetricsSink>(
            static services => services.GetRequiredService<ProxyMetrics>());
        services.AddSingleton<PrometheusMetricsExporter>();
        services.AddSingleton<IProxyMetricsExportAvailabilityReader, ProxyMetricsExportAvailabilityReader>();
        services.AddSingleton<ProxyMetricsExportAvailabilityService>();
        services.AddSingleton<IProxyMetricsExportProvider, ProxyMetricsExportProvider>();
        services.AddSingleton<ProxyMetricsAdministrationService>();
        services.AddSingleton<IProxyLogPersistenceSettingsReader, ProxyLogPersistenceSettingsReader>();
        services.AddSingleton<ProxyPersistentLogWriter>();
        services.AddSingleton<IProxyLogPersistenceStore>(static services => services.GetRequiredService<ProxyPersistentLogWriter>());
        services.AddSingleton<AccessLogEmitter>();
    }

    private static void AddProxyAdministrationServices(this IServiceCollection services)
    {
        services.AddSingleton<IProxyBackupFileSystem, ProxyBackupFileSystem>();
        services.AddSingleton<ProxyBackupService>();
        services.AddSingleton<IProxyBackupOperations>(static services => services.GetRequiredService<ProxyBackupService>());
        services.AddSingleton<ProxyBackupAdministrationService>();
        services.AddSingleton<IProxyConfigLintActiveConfigurationSource, ProxyConfigLintActiveConfigurationSource>();
        services.AddSingleton<IProxyConfigLintSubmittedConfigurationSource, ProxyConfigLintSubmittedConfigurationSource>();
        services.AddSingleton<IProxyConfigLintRuntimeStateSource, ProxyConfigLintRuntimeStateSource>();
        services.AddSingleton<IProxyConfigLintMetricsSink>(static services => services.GetRequiredService<ProxyMetrics>());
        services.AddSingleton<IProxyConfigLintSourceNameFormatter, ProxyConfigLintSourceNameFormatter>();
        services.AddSingleton<ConfigLintService>();
        services.AddSingleton<IProxyConfigLintOperations>(static services => services.GetRequiredService<ConfigLintService>());
        services.AddSingleton<ProxyConfigLintAdministrationService>();
        services.AddSingleton<IProxyRouteDiagnosticsConfigurationSource, ProxyRouteDiagnosticsConfigurationSource>();
        services.AddSingleton<IProxyRouteDiagnosticsMatcher, ProxyRouteDiagnosticsMatcher>();
        services.AddSingleton<IProxyRouteDiagnosticsActionPolicy, ProxyRouteDiagnosticsActionPolicyAdapter>();
        services.AddSingleton<IProxyRouteDiagnosticsPathRewritePolicy, ProxyRouteDiagnosticsPathRewritePolicyAdapter>();
        services.AddSingleton<IProxyRouteDiagnosticsMetricsSink>(static services => services.GetRequiredService<ProxyMetrics>());
        services.AddSingleton<IProxyClientAddressSyntaxPolicy, ProxyClientAddressSyntaxPolicy>();
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
        services.AddSingleton<IProxyRequestDiagnosticsSource>(
            static services => services.GetRequiredService<RecentRequestDiagnosticsStore>());
        services.AddSingleton<IProxyRequestDiagnosticsReader, ProxyRequestDiagnosticsReader>();
        services.AddSingleton<IProxyRequestIdRuntimeIdentitySource, SystemRequestIdRuntimeIdentitySource>();
        services.AddSingleton<ProxyDiagnosticsAdministrationService>();
        services.AddSingleton<AdminAuditStore>();
        services.AddSingleton<IProxyAdminAuditReader>(static services => services.GetRequiredService<AdminAuditStore>());
        services.AddSingleton<IProxyAdminAuditRecorder>(static services => services.GetRequiredService<AdminAuditStore>());
        services.AddSingleton<ProxyAdminAuditAdministrationService>();
        services.AddSingleton<IProxyAdminSecurityOptionsReader, ProxyAdminSecurityOptionsReader>();
        services.AddSingleton<IProxyAdminAuthenticationEventSink, AdminAuthenticationLogger>();
        services.AddSingleton<ProxyAdminAuthenticationService>();
    }

    private static void AddProxyAcmeServices(this IServiceCollection services)
    {
        services.AddSingleton<AcmeChallengeStore>();
        services.AddSingleton<AcmeHttp01ChallengeResponder>();
        services.AddSingleton<AcmeCertificateStatusStore>();
        services.AddSingleton<IAcmeCertificateMaterialWriter, AcmeCertificateMaterialWriter>();
        services.AddSingleton<IAcmeCertificateRenewalEventSink, AcmeCertificateRenewalLogger>();
        services.AddSingleton<IProxyAcmeStatusConfigurationSource, ProxyAcmeStatusConfigurationSource>();
        services.AddSingleton<IProxyAcmeCertificateLifecycleStatusSource, ProxyAcmeCertificateLifecycleStatusSource>();
        services.AddSingleton<IProxyAcmeStatusSnapshotReader, ProxyAcmeStatusSnapshotReader>();
        services.AddSingleton<ProxyAcmeAdministrationService>();
        services.AddSingleton<IAcmeCertificateIssuer, DisabledAcmeCertificateIssuer>();
        services.AddSingleton<AcmeRenewalSchedulePolicy>();
        services.AddSingleton<AcmeCertificateManager>();
    }

    private static void AddProxyRuntimeServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ProxyListenerReloadPlanner>();
        services.AddSingleton<IRuntimeHttp3PlatformSupportSource, SystemRuntimeHttp3PlatformSupportSource>();
        services.AddSingleton<ProxyListenerService>();
        services.AddSingleton<IProxyListenerReloadApplier>(static services => services.GetRequiredService<ProxyListenerService>());
        services.AddSingleton<ProxyAdmissionController>();
        services.AddSingleton<IProxyRuntimeDirectoryProbe, ProxyRuntimeDirectoryProbe>();
        services.AddSingleton<ProxyRuntimePreflightService>();
        services.AddSingleton<IProxyStatusConfigurationSource>(static services => services.GetRequiredService<ProxyConfigurationStore>());
        services.AddSingleton<IProxyStatusMetricsSource>(static services => services.GetRequiredService<ProxyMetrics>());
        services.AddSingleton<IProxyStatusRuntimePreflightSource>(static services => services.GetRequiredService<ProxyRuntimePreflightService>());
        services.AddSingleton<IProxyStatusInputReader, ProxyStatusInputReader>();
        services.AddSingleton<IProxyStatusOperations, ProxyStatusOperations>();
        services.AddSingleton<ProxyStatusAdministrationService>();
        services.AddSingleton<CircuitBreakerStore>();
        services.AddSingleton<ProxyShutdownCoordinator>();
        services.AddSingleton<ClientRateLimiter>();
        services.AddSingleton<ProxyRuntimeState>();
        services.AddSingleton<IProxyStatusRuntimeStateSource>(static services => services.GetRequiredService<ProxyRuntimeState>());
        services.AddSingleton<UpstreamHealthStore>();
        services.AddSingleton<IProxyStatusUpstreamHealthSource>(static services => services.GetRequiredService<UpstreamHealthStore>());
    }

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

    private static void AddProxyHostedServices(this IServiceCollection services)
    {
        services.AddHostedService<ProxyConfigurationStartupService>();
        services.AddHostedService<AcmeRenewalService>();
        services.AddHostedService<UpstreamHealthCheckService>();
        services.AddHostedService(static services => services.GetRequiredService<ProxyListenerService>());
    }
}
