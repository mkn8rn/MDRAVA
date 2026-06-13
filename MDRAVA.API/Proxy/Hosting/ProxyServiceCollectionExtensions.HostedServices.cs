using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.HealthChecks;
using MDRAVA.INF.Acme;
using MDRAVA.INF.Configuration.Loading;
using MDRAVA.INF.Proxy.Hosting;
using MDRAVA.INF.Proxy.Health;

namespace MDRAVA.API.Proxy.Hosting;

public static partial class ProxyServiceCollectionExtensions
{
    private static void AddProxyHostedServices(this IServiceCollection services)
    {
        services.AddHostedService<ProxyConfigurationStartupService>();
        services.AddHostedService<AcmeRenewalService>();
        services.AddHostedService<UpstreamHealthCheckService>();
        services.AddHostedService(static services => services.GetRequiredService<ProxyListenerService>());
    }
}
