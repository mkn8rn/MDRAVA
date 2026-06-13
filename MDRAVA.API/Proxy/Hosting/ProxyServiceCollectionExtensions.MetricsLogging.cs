using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.AdminAuthentication;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;
using MDRAVA.BLL.ControlPlane.HealthChecks;
using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.ControlPlane.Metrics;
using MDRAVA.BLL.ControlPlane.Observability;
using MDRAVA.BLL.ControlPlane.RequestDiagnostics;
using MDRAVA.BLL.ControlPlane.Resilience;
using MDRAVA.BLL.ControlPlane.RuntimeGuards;
using MDRAVA.BLL.ControlPlane.UpstreamSelection;
using MDRAVA.INF.Observability;

namespace MDRAVA.API.Proxy.Hosting;

public static partial class ProxyServiceCollectionExtensions
{
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
        services.AddSingleton<
            IProxyMetricsExportConfigurationSource,
            ProxyConfigurationMetricsExportConfigurationSource>();
        services.AddSingleton<IProxyMetricsExportAvailabilityReader, ProxyMetricsExportAvailabilityReader>();
        services.AddSingleton<ProxyMetricsExportAvailabilityService>();
        services.AddSingleton<IProxyMetricsExportInputSource, ProxyMetricsExportInputSource>();
        services.AddSingleton<IProxyMetricsExportProvider, ProxyMetricsExportProvider>();
        services.AddSingleton<ProxyMetricsAdministrationService>();
        services.AddSingleton<IProxyLogPersistenceSettingsSource, ProxyConfigurationLogPersistenceSettingsSource>();
        services.AddSingleton<IProxyLogPersistenceSettingsReader, ProxyLogPersistenceSettingsReader>();
        services.AddSingleton<ProxyPersistentLogWriter>();
        services.AddSingleton<IProxyLogPersistenceStore>(static services => services.GetRequiredService<ProxyPersistentLogWriter>());
        services.AddSingleton<AccessLogEmitter>();
    }
}
