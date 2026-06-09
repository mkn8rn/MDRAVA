using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane;
using MDRAVA.BLL.Infrastructure;

namespace MDRAVA.INF.Runtime;

public sealed class ProxyAdminSecurityOptionsReader : IProxyAdminSecurityOptionsReader
{
    private readonly IProxyConfigurationStore _configurationStore;

    public ProxyAdminSecurityOptionsReader(IProxyConfigurationStore configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public ProxyAdminSecurityOptionsReadResult Read()
    {
        if (_configurationStore.TryGetSnapshot(out var snapshot) && snapshot is not null)
        {
            return new ProxyAdminSecurityOptionsReadResult(
                true,
                snapshot.AdminSecurity);
        }

        var adminOptions = new ProxyAdminOptions();
        return new ProxyAdminSecurityOptionsReadResult(
            false,
            ProxyConfigurationRuntimeMapper.ToRuntimeAdminSecurityOptions(
                adminOptions,
                ProxyAdminSecurityTokenPolicy.Resolve(adminOptions, Environment.GetEnvironmentVariable)));
    }
}
