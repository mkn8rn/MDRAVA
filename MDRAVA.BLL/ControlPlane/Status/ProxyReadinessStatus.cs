using MDRAVA.BLL.ControlPlane;
namespace MDRAVA.BLL.ControlPlane.Status;

public sealed record ProxyReadinessStatus(
    string State,
    IReadOnlyList<string> Reasons,
    DateTimeOffset GeneratedAtUtc,
    int? ConfigGeneration)
{
    public static ProxyReadinessStatus Unknown { get; } = new(
        ProxyStatusText.Unknown,
        [ProxyStatusText.NotAvailable],
        DateTimeOffset.UnixEpoch,
        null);
}
