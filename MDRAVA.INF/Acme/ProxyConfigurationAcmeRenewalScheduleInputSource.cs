using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.INF.Acme;

public sealed class ProxyConfigurationAcmeRenewalScheduleInputSource : IAcmeRenewalScheduleInputSource
{
    private readonly IProxyActiveConfigurationSnapshotReader _configurationStore;

    public ProxyConfigurationAcmeRenewalScheduleInputSource(IProxyActiveConfigurationSnapshotReader configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public AcmeRenewalScheduleInputReadResult ReadInput()
    {
        return _configurationStore.ReadSnapshot() is ProxyConfigurationSnapshotReadResult.AvailableResult available
            ? AcmeRenewalScheduleInputReadResult.Available(
                AcmeRenewalScheduleInputMapper.FromSource(
                    AcmeRenewalScheduleSourceMapper.FromSource(available.Snapshot.Acme)))
            : AcmeRenewalScheduleInputReadResult.MissingConfiguration;
    }
}
