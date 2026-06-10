namespace MDRAVA.BLL.ControlPlane.AdminAudit;

public interface IProxyAdminAuditReader
{
    IReadOnlyList<ProxyAdminAuditEvent> Recent(int limit);
}
