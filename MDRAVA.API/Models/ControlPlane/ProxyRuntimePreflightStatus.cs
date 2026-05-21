namespace MDRAVA.API.Models.ControlPlane;

public sealed record ProxyRuntimePreflightStatus(
    string State,
    DateTimeOffset? GeneratedAtUtc,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<ProxyRuntimePreflightCheck> Checks)
{
    public static ProxyRuntimePreflightStatus Unknown { get; } = new(ProxyStatusText.Unknown, null, [], []);
}

public sealed record ProxyRuntimePreflightCheck(
    string Name,
    string RelativePath,
    bool Exists,
    bool Created,
    bool CanRead,
    bool CanWrite,
    string Severity,
    string Reason);
