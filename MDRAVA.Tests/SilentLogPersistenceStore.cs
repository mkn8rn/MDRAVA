using MDRAVA.BLL.ControlPlane.AdminAudit;
using MDRAVA.BLL.ControlPlane.Observability;
using MDRAVA.BLL.ControlPlane.Status;

namespace MDRAVA.Tests;

internal sealed class SilentLogPersistenceStore : IProxyLogPersistenceStore
{
    public static SilentLogPersistenceStore Instance { get; } = new();

    private SilentLogPersistenceStore()
    {
    }

    public void WriteAccess(ProxyAccessLogEntry entry)
    {
    }

    public void WriteAdminAudit(ProxyAdminAuditEvent auditEvent)
    {
    }

    public ProxyLogPersistenceStatus GetStatus()
    {
        return ProxyLogPersistenceStatus.Unknown;
    }
}
