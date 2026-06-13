using BusinessRuntimeAdminSecurityProjection = MDRAVA.BLL.Configuration.RuntimeAdminSecurityProjection;

namespace MDRAVA.API.Controllers;

public sealed record RuntimeAdminSecurityResponse(
    IReadOnlyList<string> Urls,
    bool RequireAuthentication,
    bool HasConfiguredToken,
    string? Token,
    string TokenEnvironmentVariable,
    string TokenSource,
    int RecentAuditCapacity)
{
    public static RuntimeAdminSecurityResponse FromProjection(BusinessRuntimeAdminSecurityProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeAdminSecurityResponse(
            projection.Urls.ToArray(),
            projection.RequireAuthentication,
            projection.HasConfiguredToken,
            projection.Token,
            projection.TokenEnvironmentVariable,
            projection.TokenSource,
            projection.RecentAuditCapacity);
    }
}
