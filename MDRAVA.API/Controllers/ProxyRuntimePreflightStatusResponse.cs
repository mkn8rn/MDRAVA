using MDRAVA.BLL.ControlPlane.Status;

using BusinessProxyRuntimePreflightCheck = MDRAVA.BLL.ControlPlane.Status.ProxyRuntimePreflightCheck;
using BusinessProxyRuntimePreflightStatus = MDRAVA.BLL.ControlPlane.Status.ProxyRuntimePreflightStatus;

namespace MDRAVA.API.Controllers;

public sealed record ProxyRuntimePreflightStatusResponse(
    string State,
    DateTimeOffset? GeneratedAtUtc,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<ProxyRuntimePreflightCheckResponse> Checks)
{
    public static ProxyRuntimePreflightStatusResponse FromStatus(BusinessProxyRuntimePreflightStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return new ProxyRuntimePreflightStatusResponse(
            status.State,
            status.GeneratedAtUtc,
            status.Reasons,
            ProxyRuntimePreflightCheckResponse.FromChecks(status.Checks));
    }
}

public sealed record ProxyRuntimePreflightCheckResponse(
    string Name,
    string RelativePath,
    bool Exists,
    bool Created,
    bool CanRead,
    bool CanWrite,
    string Severity,
    string Reason)
{
    public static IReadOnlyList<ProxyRuntimePreflightCheckResponse> FromChecks(
        IReadOnlyList<BusinessProxyRuntimePreflightCheck> checks)
    {
        ArgumentNullException.ThrowIfNull(checks);

        if (checks.Count == 0)
        {
            return [];
        }

        var responses = new List<ProxyRuntimePreflightCheckResponse>(checks.Count);
        foreach (var check in checks)
        {
            responses.Add(FromCheck(check));
        }

        return responses;
    }

    public static ProxyRuntimePreflightCheckResponse FromCheck(BusinessProxyRuntimePreflightCheck check)
    {
        ArgumentNullException.ThrowIfNull(check);

        return new ProxyRuntimePreflightCheckResponse(
            check.Name,
            check.RelativePath,
            check.Exists,
            check.Created,
            check.CanRead,
            check.CanWrite,
            check.Severity,
            check.Reason);
    }
}
