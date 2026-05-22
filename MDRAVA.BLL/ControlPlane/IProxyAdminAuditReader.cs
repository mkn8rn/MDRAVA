namespace MDRAVA.BLL.ControlPlane;

public interface IProxyAdminAuditReader
{
    IReadOnlyList<ProxyAdminAuditEvent> Recent(int limit);
}
