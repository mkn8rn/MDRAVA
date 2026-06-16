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
            ApiResponseList.Copy(status.Reasons),
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
        IEnumerable<BusinessProxyRuntimePreflightCheck> checks)
    {
        return ApiResponseList.Copy(checks.Select(FromCheck));
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
