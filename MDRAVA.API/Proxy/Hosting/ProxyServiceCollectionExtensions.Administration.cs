using MDRAVA.API.Proxy.Security;
using MDRAVA.BLL.ControlPlane.AdminAudit;
using MDRAVA.BLL.ControlPlane.AdminAuthentication;
using MDRAVA.BLL.ControlPlane.Backup;
using MDRAVA.BLL.ControlPlane.Caching;
using MDRAVA.BLL.ControlPlane.ConfigLint;
using MDRAVA.BLL.ControlPlane.Metrics;
using MDRAVA.BLL.ControlPlane.RequestDiagnostics;
using MDRAVA.BLL.ControlPlane.RouteDiagnostics;
using MDRAVA.BLL.ControlPlane.RuntimeGuards;
using MDRAVA.INF.Configuration.Loading;
using MDRAVA.INF.DataDirectory;
using MDRAVA.INF.Observability;
using MDRAVA.INF.Proxy.RuntimeGuards;
using MDRAVA.INF.Runtime;

namespace MDRAVA.API.Proxy.Hosting;

public static partial class ProxyServiceCollectionExtensions
{
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
}
