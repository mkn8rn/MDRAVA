using MDRAVA.BLL.ControlPlane;

namespace MDRAVA.BLL.Infrastructure;

public interface IProxyLogPersistenceStore
{
    void WriteAccess(ProxyAccessLogEntry entry);

    void WriteAdminAudit(ProxyAdminAuditEvent auditEvent);

    ProxyLogPersistenceStatus GetStatus();
}
