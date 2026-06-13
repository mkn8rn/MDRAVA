namespace MDRAVA.API.Proxy.Hosting;

public static partial class ProxyServiceCollectionExtensions
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
}
