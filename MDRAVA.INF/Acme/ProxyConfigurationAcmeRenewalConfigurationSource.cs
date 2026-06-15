using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.INF.Acme;

public sealed class ProxyConfigurationAcmeRenewalConfigurationSource : IAcmeRenewalConfigurationSource
{
    private readonly IProxyActiveConfigurationSnapshotReader _configurationStore;

    public ProxyConfigurationAcmeRenewalConfigurationSource(IProxyActiveConfigurationSnapshotReader configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public AcmeRenewalConfigurationInputReadResult ReadInput()
    {
        var snapshotResult = _configurationStore.ReadSnapshot();
        if (snapshotResult is not ProxyConfigurationSnapshotReadResult.AvailableResult available)
        {
            return AcmeRenewalConfigurationInputReadResult.MissingConfiguration;
        }

        var snapshot = available.Snapshot;
        return AcmeRenewalConfigurationInputReadResult.Available(
            AcmeRenewalConfigurationInputMapper.FromSources(
                AcmeRenewalConfigurationSourceMapper.FromRuntimeConfiguration(
                    snapshot.Acme,
                    snapshot.Certificates)));
    }
}
