using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed record AcmeRenewalScheduleInput(
    bool Enabled,
    int CheckIntervalMinutes);

public interface IAcmeRenewalScheduleInputSource
{
    AcmeRenewalScheduleInput? ReadInput();
}

public static class AcmeRenewalScheduleInputMapper
{
    public static AcmeRenewalScheduleInput? FromSnapshot(ProxyConfigurationSnapshot? snapshot)
    {
        return snapshot is null
            ? null
            : new AcmeRenewalScheduleInput(
                snapshot.Acme.Enabled,
                snapshot.Acme.CheckIntervalMinutes);
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
        _configurationStore.TryGetSnapshot(out var snapshot);
        return AcmeRenewalScheduleInputMapper.FromSnapshot(snapshot);
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
