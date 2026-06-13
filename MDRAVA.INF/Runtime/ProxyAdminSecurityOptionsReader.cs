using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.AdminAuthentication;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

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
        if (_configurationStore.ReadSnapshot() is ProxyConfigurationSnapshotReadResult.AvailableResult available)
        {
            var snapshot = available.Snapshot;
            return ProxyAdminSecurityOptionsReadResult.FromActiveConfiguration(
                snapshot.AdminSecurity.RequireAuthentication,
                snapshot.AdminSecurity.Token,
                snapshot.AdminSecurity.RecentAuditCapacity);
        }

        var adminOptions = new ProxyAdminOptions();
        var security = ProxyConfigurationRuntimeMapper.ToRuntimeAdminSecurityOptions(
            adminOptions,
            ProxyAdminSecurityTokenPolicy.Resolve(adminOptions, Environment.GetEnvironmentVariable));
        return ProxyAdminSecurityOptionsReadResult.FromDefaults(
            security.RequireAuthentication,
            security.Token,
            security.RecentAuditCapacity);
    }
}
