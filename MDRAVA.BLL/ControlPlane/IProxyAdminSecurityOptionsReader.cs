using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane;

public interface IProxyAdminSecurityOptionsReader
{
    ProxyAdminSecurityOptionsReadResult Read();
}

public sealed record ProxyAdminSecurityOptionsReadResult(
    bool HasActiveConfiguration,
    RuntimeAdminSecurityOptions Security);
