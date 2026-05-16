namespace MDRAVA.API.Proxy.Forwarding;

public enum ProxyTimeoutKind
{
    ClientRequestHead,
    ClientKeepAliveIdle,
    ClientRequestBodyIdle,
    UpstreamConnect,
    UpstreamResponseHead,
    UpstreamResponseBodyIdle,
    DownstreamWrite,
    TlsHandshake
}
