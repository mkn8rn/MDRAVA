using MDRAVA.API.Proxy.Configuration;
using MDRAVA.API.Proxy.Configuration.Loading;
using MDRAVA.API.Proxy.Configuration.Paths;
using MDRAVA.API.Proxy.Configuration.Storage;
using MDRAVA.API.Proxy.Connections;
using MDRAVA.API.Proxy.Forwarding;
using MDRAVA.API.Proxy.Metrics;
using MDRAVA.API.Proxy.Routing;
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
        services.AddSingleton<IProxyConfigurationStore, ProxyConfigurationStore>();
        services.AddSingleton<IProxyConfigurationLoader, ProxyConfigurationLoader>();
        services.AddSingleton<IProxyConfigurationReloadService, ProxyConfigurationReloadService>();
        services.AddSingleton<ProxyMetrics>();
        services.AddSingleton<ProxyRuntimeState>();
        services.AddSingleton<IRouteMatcher, SingleUpstreamRouteMatcher>();
        services.AddSingleton<UpstreamConnectionFactory>();
        services.AddSingleton<UpstreamConnectionPool>();
        services.AddSingleton<HopByHopHeaderPolicy>();
        services.AddSingleton<UpgradeRequestPolicy>();
        services.AddSingleton<TunnelRelay>();
        services.AddSingleton<ProxyForwarder>();
        services.AddSingleton<UpgradeForwarder>();
        services.AddSingleton<TlsConnectionAuthenticator>();
        services.AddHostedService<ProxyConfigurationStartupService>();
        services.AddHostedService<ProxyListenerService>();

        return services;
    }
}
