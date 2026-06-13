using MDRAVA.BLL.ControlPlane.Caching;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;
using MDRAVA.BLL.ControlPlane.HealthChecks;
using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.ControlPlane.Listeners;
using MDRAVA.BLL.ControlPlane.Metrics;
using MDRAVA.BLL.ControlPlane.Resilience;
using MDRAVA.BLL.ControlPlane.RuntimeGuards;
using MDRAVA.BLL.ControlPlane.RuntimePreflight;
using MDRAVA.BLL.ControlPlane.Status;
using MDRAVA.INF.Proxy.Hosting;
using MDRAVA.INF.Proxy.Http3;
using MDRAVA.INF.Proxy.RuntimeGuards;
using MDRAVA.INF.Runtime;

namespace MDRAVA.API.Proxy.Hosting;

public static partial class ProxyServiceCollectionExtensions
{
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
        services.AddSingleton<IHttp3AltSvcRuntimeListenerSource>(static services => services.GetRequiredService<ProxyRuntimeState>());
        services.AddSingleton<UpstreamHealthStore>();
        services.AddSingleton<IProxyStatusUpstreamHealthSource>(static services => services.GetRequiredService<UpstreamHealthStore>());
        services.AddSingleton<IProxyStatusUpstreamHealthReader, ProxyStatusUpstreamHealthReader>();
    }
}
