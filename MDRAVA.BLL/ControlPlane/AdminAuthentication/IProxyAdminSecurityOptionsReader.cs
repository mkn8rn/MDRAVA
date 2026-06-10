using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.AdminAuthentication;

public interface IProxyAdminSecurityOptionsReader
{
    ProxyAdminSecurityOptionsReadResult Read();
}

public sealed record ProxyAdminSecurityOptionsReadResult(
    bool HasActiveConfiguration,
    RuntimeAdminSecurityOptions Security);
