using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.INF.Acme;

public static class AcmeRenewalScheduleSourceMapper
{
    public static AcmeRenewalScheduleSource FromRuntimeConfiguration(RuntimeAcmeOptions acme)
    {
        return new AcmeRenewalScheduleSource(
            acme.Enabled,
            acme.CheckIntervalMinutes);
    }
}

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
                    AcmeRenewalScheduleSourceMapper.FromRuntimeConfiguration(available.Snapshot.Acme)))
            : AcmeRenewalScheduleInputReadResult.MissingConfiguration;
    }
}
