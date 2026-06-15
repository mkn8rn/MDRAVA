using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Listeners;

public sealed record TcpAlpnAdvertisementInput(bool Http1Enabled, bool Http2Enabled);

public sealed record Http3AlpnAdvertisementInput(bool EnabledForTraffic);

public static class ListenerProtocolAdvertisementInputMapper
{
    public static TcpAlpnAdvertisementInput FromTcpRuntimeProtocols(RuntimeListenerProtocols protocols)
    {
        return new TcpAlpnAdvertisementInput(
            protocols.HasFlag(RuntimeListenerProtocols.Http1),
            protocols.HasFlag(RuntimeListenerProtocols.Http2));
    }

    public static Http3AlpnAdvertisementInput FromHttp3RuntimeListener(RuntimeListener listener)
    {
        return new Http3AlpnAdvertisementInput(listener.Http3.EnabledForTraffic);
    }
}

public static class ListenerProtocolAdvertisementPolicy
{
    public const string Http1Alpn = "http/1.1";
    public const string Http2Alpn = "h2";
    public const string Http3Alpn = "h3";

    public static IReadOnlyList<string> BuildTcpAlpnProtocolNames(TcpAlpnAdvertisementInput input)
    {
        List<string> advertised = [];
        if (input.Http2Enabled)
        {
            advertised.Add(Http2Alpn);
        }

        if (input.Http1Enabled)
        {
            advertised.Add(Http1Alpn);
        }

        return advertised;
    }

    public static IReadOnlyList<string> BuildHttp3AlpnProtocolNames(Http3AlpnAdvertisementInput input)
    {
        return input.EnabledForTraffic
            ? [Http3Alpn]
            : [];
    }
}
