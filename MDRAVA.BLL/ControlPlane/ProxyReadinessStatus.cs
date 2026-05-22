namespace MDRAVA.BLL.ControlPlane;

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
