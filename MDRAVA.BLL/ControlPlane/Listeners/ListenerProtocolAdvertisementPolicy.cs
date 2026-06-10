using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Listeners;

public static class ListenerProtocolAdvertisementPolicy
{
    public const string Http1Alpn = "http/1.1";
    public const string Http2Alpn = "h2";
    public const string Http3Alpn = "h3";

    public static IReadOnlyList<string> BuildTcpAlpnProtocolNames(RuntimeListenerProtocols protocols)
    {
        List<string> advertised = [];
        if (protocols.HasFlag(RuntimeListenerProtocols.Http2))
        {
            advertised.Add(Http2Alpn);
        }

        if (protocols.HasFlag(RuntimeListenerProtocols.Http1))
        {
            advertised.Add(Http1Alpn);
        }

        return advertised;
    }

    public static IReadOnlyList<string> BuildHttp3AlpnProtocolNames(RuntimeListener listener)
    {
        return listener.Http3.EnabledForTraffic
            ? [Http3Alpn]
            : [];
    }
}
