namespace MDRAVA.BLL.ControlPlane.AdminAuthentication;

public interface IProxyAdminSecurityOptionsReader
{
    ProxyAdminSecurityOptionsReadResult Read();
}

public sealed record ProxyAdminSecurityOptionsReadResult(
    bool HasActiveConfiguration,
    bool RequireAuthentication,
    string? Token,
    int RecentAuditCapacity);
