using BusinessProxyAdminAuditEvent = MDRAVA.BLL.ControlPlane.AdminAudit.ProxyAdminAuditEvent;

namespace MDRAVA.API.Controllers;

public sealed record ProxyAdminAuditEventResponse(
    DateTimeOffset TimestampUtc,
    string Method,
    string Path,
    string? ClientIp,
    string AuthResult,
    int StatusCode,
    bool Succeeded)
{
    public static IReadOnlyList<ProxyAdminAuditEventResponse> FromEvents(
        IReadOnlyList<BusinessProxyAdminAuditEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        return ApiResponseList.Copy(events.Select(FromEvent));
    }

    private static ProxyAdminAuditEventResponse FromEvent(BusinessProxyAdminAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        return new ProxyAdminAuditEventResponse(
            auditEvent.TimestampUtc,
            auditEvent.Method,
            auditEvent.Path,
            auditEvent.ClientIp,
            auditEvent.AuthResult,
            auditEvent.StatusCode,
            auditEvent.Succeeded);
    }
}
