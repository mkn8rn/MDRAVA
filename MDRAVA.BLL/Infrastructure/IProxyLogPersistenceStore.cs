using MDRAVA.BLL.ControlPlane.Status;
using MDRAVA.BLL.ControlPlane.AdminAudit;
using MDRAVA.BLL.ControlPlane.Observability;

namespace MDRAVA.BLL.Infrastructure;

public interface IProxyLogPersistenceStore
{
    void WriteAccess(ProxyAccessLogEntry entry);

    void WriteAdminAudit(ProxyAdminAuditEvent auditEvent);

    ProxyLogPersistenceStatus GetStatus();
}
