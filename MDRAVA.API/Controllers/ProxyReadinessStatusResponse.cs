using BusinessProxyReadinessStatus = MDRAVA.BLL.ControlPlane.Status.ProxyReadinessStatus;

namespace MDRAVA.API.Controllers;

public sealed record ProxyReadinessStatusResponse
{
    public ProxyReadinessStatusResponse(
        string state,
        IReadOnlyList<string> reasons,
        DateTimeOffset generatedAtUtc,
        int? configGeneration)
    {
        State = state;
        Reasons = ApiResponseList.Copy(reasons);
        GeneratedAtUtc = generatedAtUtc;
        ConfigGeneration = configGeneration;
    }

    public string State { get; }

    public IReadOnlyList<string> Reasons { get; }

    public DateTimeOffset GeneratedAtUtc { get; }

    public int? ConfigGeneration { get; }

    public static ProxyReadinessStatusResponse FromStatus(BusinessProxyReadinessStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return new ProxyReadinessStatusResponse(
            state: status.State,
            reasons: status.Reasons,
            generatedAtUtc: status.GeneratedAtUtc,
            configGeneration: status.ConfigGeneration);
    }
}
