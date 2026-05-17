using System.Net.Security;

namespace MDRAVA.API.Proxy.Tls;

public static class ListenerProtocolAdvertisement
{
    public static List<SslApplicationProtocol> BuildTcpAlpn(RuntimeListenerProtocols protocols)
    {
        List<SslApplicationProtocol> advertised = [];
        if (protocols.HasFlag(RuntimeListenerProtocols.Http2))
        {
            advertised.Add(SslApplicationProtocol.Http2);
        }

        if (protocols.HasFlag(RuntimeListenerProtocols.Http1))
        {
            advertised.Add(SslApplicationProtocol.Http11);
        }

        return advertised;
    }

    public static IReadOnlyList<SslApplicationProtocol> FutureHttp3Alpn(RuntimeListenerProtocols protocols)
    {
        return protocols.HasHttp3Preview()
            ? [new SslApplicationProtocol("h3")]
            : [];
    }

    public static List<SslApplicationProtocol> BuildHttp3PreviewAlpn(RuntimeListenerProtocols protocols)
    {
        return protocols.HasHttp3Preview()
            ? [new SslApplicationProtocol("h3")]
            : [];
    }

    public static string ToConfigText(RuntimeListenerProtocols protocols)
    {
        return protocols switch
        {
            RuntimeListenerProtocols.Http2 => "http2",
            RuntimeListenerProtocols.Http1AndHttp2 => "http1AndHttp2",
            RuntimeListenerProtocols.Http3Preview => "http3Preview",
            RuntimeListenerProtocols.Http1AndHttp3Preview => "http1AndHttp3Preview",
            RuntimeListenerProtocols.Http2AndHttp3Preview => "http2AndHttp3Preview",
            RuntimeListenerProtocols.Http1AndHttp2AndHttp3Preview => "http1AndHttp2AndHttp3Preview",
            _ => "http1"
        };
    }
}
