using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Backup;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;
using MDRAVA.BLL.ControlPlane.Headers;
using MDRAVA.INF.Configuration;
using MDRAVA.INF.Configuration.Loading;
using MDRAVA.INF.Configuration.Paths;
using MDRAVA.INF.DataDirectory;
using MDRAVA.INF.Observability;
using MDRAVA.INF.Proxy.Forwarding;
using MDRAVA.INF.Runtime;
using Microsoft.Extensions.Options;

namespace MDRAVA.API.Proxy.Hosting;

public static partial class ProxyServiceCollectionExtensions
{
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
}
