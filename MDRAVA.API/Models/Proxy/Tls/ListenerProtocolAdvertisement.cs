using System.Net.Security;

namespace MDRAVA.API.Proxy.Tls;

public static class ListenerProtocolAdvertisement
{
    public static List<SslApplicationProtocol> BuildTcpAlpn(RuntimeListenerProtocols protocols)
    {
        return ListenerProtocolAdvertisementPolicy.BuildTcpAlpnProtocolNames(protocols)
            .Select(static protocol => new SslApplicationProtocol(protocol))
            .ToList();
    }

    public static List<SslApplicationProtocol> BuildHttp3Alpn(RuntimeListener listener)
    {
        return ListenerProtocolAdvertisementPolicy.BuildHttp3AlpnProtocolNames(listener)
            .Select(static protocol => new SslApplicationProtocol(protocol))
            .ToList();
    }
}
