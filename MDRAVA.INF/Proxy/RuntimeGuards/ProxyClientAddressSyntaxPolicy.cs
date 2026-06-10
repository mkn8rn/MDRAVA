using System.Net;
using MDRAVA.BLL.ControlPlane.RuntimeGuards;

namespace MDRAVA.INF.Proxy.RuntimeGuards;

public sealed class ProxyClientAddressSyntaxPolicy : IProxyClientAddressSyntaxPolicy
{
    public bool IsIpLiteral(string value)
    {
        return IPAddress.TryParse(value, out _);
    }
}
