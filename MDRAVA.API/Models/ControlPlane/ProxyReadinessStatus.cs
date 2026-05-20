namespace MDRAVA.API.Models.ControlPlane;

public sealed record ProxyReadinessStatus(
    string State,
    IReadOnlyList<string> Reasons,
    DateTimeOffset GeneratedAtUtc,
    int? ConfigGeneration)
{
    public static ProxyReadinessStatus Unknown { get; } = new(
        "unknown",
        ["not_available"],
        DateTimeOffset.UnixEpoch,
        null);
}
