using MDRAVA.BLL.ControlPlane.Status;
using MDRAVA.BLL.ControlPlane.AdminAudit;

namespace MDRAVA.BLL.ControlPlane.Observability;

public interface IProxyLogPersistenceStore
{
    void WriteAccess(ProxyAccessLogEntry entry);

    void WriteAdminAudit(ProxyAdminAuditEvent auditEvent);

    ProxyLogPersistenceStatus GetStatus();
}
