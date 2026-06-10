namespace MDRAVA.BLL.ControlPlane.AdminAudit;

public interface IProxyAdminAuditRecorder
{
    void Add(ProxyAdminAuditEvent auditEvent, int capacity);
}
