using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed record AcmeRenewalScheduleInput(
    bool Enabled,
    int CheckIntervalMinutes);

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
