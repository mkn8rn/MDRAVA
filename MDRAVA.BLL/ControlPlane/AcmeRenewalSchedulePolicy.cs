using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane;

public sealed class AcmeRenewalSchedulePolicy
{
    public TimeSpan ResolveDelay(ProxyConfigurationSnapshot? snapshot)
    {
        if (snapshot is not null && snapshot.Acme.Enabled)
        {
            return TimeSpan.FromMinutes(Math.Clamp(snapshot.Acme.CheckIntervalMinutes, 5, 1440));
        }

        return TimeSpan.FromHours(12);
    }
}
