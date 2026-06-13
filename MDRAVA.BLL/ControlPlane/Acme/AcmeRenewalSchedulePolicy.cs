using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed record AcmeRenewalScheduleInput(
    bool Enabled,
    int CheckIntervalMinutes);

public sealed record AcmeRenewalScheduleSource(
    bool Enabled,
    int CheckIntervalMinutes);

public interface IAcmeRenewalScheduleInputSource
{
    AcmeRenewalScheduleInput? ReadInput();
}

public static class AcmeRenewalScheduleInputMapper
{
    public static AcmeRenewalScheduleInput FromSource(AcmeRenewalScheduleSource source)
    {
        return new AcmeRenewalScheduleInput(
            source.Enabled,
            source.CheckIntervalMinutes);
    }
}

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
    private readonly IProxyConfigurationStore _configurationStore;

    public ProxyConfigurationAcmeRenewalScheduleInputSource(IProxyConfigurationStore configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public AcmeRenewalScheduleInput? ReadInput()
    {
        return _configurationStore.ReadSnapshot() is ProxyConfigurationSnapshotReadResult.AvailableResult available
            ? AcmeRenewalScheduleInputMapper.FromSource(
                AcmeRenewalScheduleSourceMapper.FromRuntimeConfiguration(available.Snapshot.Acme))
            : null;
    }
}

public sealed class AcmeRenewalSchedulePolicy
{
    public TimeSpan ResolveDelay(AcmeRenewalScheduleInput? input)
    {
        if (input is not null && input.Enabled)
        {
            return TimeSpan.FromMinutes(Math.Clamp(input.CheckIntervalMinutes, 5, 1440));
        }

        return TimeSpan.FromHours(12);
    }
}
