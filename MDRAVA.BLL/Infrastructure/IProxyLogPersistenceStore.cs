using MDRAVA.BLL.ControlPlane;
using MDRAVA.BLL.ControlPlane.AdminAudit;

namespace MDRAVA.BLL.Infrastructure;

public interface IProxyLogPersistenceStore
{
    void WriteAccess(ProxyAccessLogEntry entry);

    void WriteAdminAudit(ProxyAdminAuditEvent auditEvent);

    ProxyLogPersistenceStatus GetStatus();
}
