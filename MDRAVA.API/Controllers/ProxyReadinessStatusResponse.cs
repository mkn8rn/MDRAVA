using MDRAVA.BLL.ControlPlane.Status;

using BusinessProxyReadinessStatus = MDRAVA.BLL.ControlPlane.Status.ProxyReadinessStatus;

namespace MDRAVA.API.Controllers;

public sealed record ProxyReadinessStatusResponse(
    string State,
    IReadOnlyList<string> Reasons,
    DateTimeOffset GeneratedAtUtc,
    int? ConfigGeneration)
{
    public static ProxyReadinessStatusResponse FromStatus(BusinessProxyReadinessStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return new ProxyReadinessStatusResponse(
            status.State,
            status.Reasons,
            status.GeneratedAtUtc,
            status.ConfigGeneration);
    }
}
