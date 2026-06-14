using System.Net.Security;
using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Listeners;

namespace MDRAVA.INF.Proxy.Tls;

public static class ListenerProtocolAdvertisement
{
    public static List<SslApplicationProtocol> BuildTcpAlpn(RuntimeListenerProtocols protocols)
    {
        return ListenerProtocolAdvertisementPolicy.BuildTcpAlpnProtocolNames(
                new TcpAlpnAdvertisementInput(
                    protocols.HasFlag(RuntimeListenerProtocols.Http1),
                    protocols.HasFlag(RuntimeListenerProtocols.Http2)))
            .Select(static protocol => new SslApplicationProtocol(protocol))
            .ToList();
    }

    public static List<SslApplicationProtocol> BuildHttp3Alpn(RuntimeListener listener)
    {
        return ListenerProtocolAdvertisementPolicy.BuildHttp3AlpnProtocolNames(
                new Http3AlpnAdvertisementInput(listener.Http3.EnabledForTraffic))
            .Select(static protocol => new SslApplicationProtocol(protocol))
            .ToList();
    }
}
