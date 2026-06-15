using System.Net.Security;
using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Listeners;

namespace MDRAVA.INF.Proxy.Tls;

public static class ListenerProtocolAdvertisement
{
    public static List<SslApplicationProtocol> BuildTcpAlpn(RuntimeListenerProtocols protocols)
    {
        return ListenerProtocolAdvertisementPolicy.BuildTcpAlpnProtocolNames(
                ListenerProtocolAdvertisementInputMapper.FromTcpRuntimeProtocols(protocols))
            .Select(static protocol => new SslApplicationProtocol(protocol))
            .ToList();
    }

    public static List<SslApplicationProtocol> BuildHttp3Alpn(RuntimeListener listener)
    {
        return ListenerProtocolAdvertisementPolicy.BuildHttp3AlpnProtocolNames(
                ListenerProtocolAdvertisementInputMapper.FromHttp3RuntimeListener(listener))
            .Select(static protocol => new SslApplicationProtocol(protocol))
            .ToList();
    }
}
