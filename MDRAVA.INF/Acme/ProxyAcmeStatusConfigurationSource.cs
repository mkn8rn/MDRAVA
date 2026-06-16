using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.INF.Acme;

public sealed class ProxyAcmeStatusConfigurationSource : IProxyAcmeStatusConfigurationSource
{
    private readonly IProxyActiveConfigurationSnapshotReader _configurationStore;

    public ProxyAcmeStatusConfigurationSource(IProxyActiveConfigurationSnapshotReader configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public ProxyAcmeStatusConfigurationSourceReadResult Read()
    {
        var snapshotResult = _configurationStore.ReadSnapshot();
        if (snapshotResult is not ProxyConfigurationSnapshotReadResult.AvailableResult available)
        {
            return ProxyAcmeStatusConfigurationSourceReadResult.MissingConfiguration;
        }

        return ProxyAcmeStatusConfigurationSourceReadResult.Available(
            ProxyAcmeStatusConfigurationSourceMapper.FromSources(
                available.Snapshot.Acme,
                available.Snapshot.Certificates));
    }
}
