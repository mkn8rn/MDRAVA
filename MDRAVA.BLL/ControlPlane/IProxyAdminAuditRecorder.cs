namespace MDRAVA.BLL.ControlPlane;

public interface IProxyAdminAuditRecorder
{
    void Add(ProxyAdminAuditEvent auditEvent, int capacity);
}
