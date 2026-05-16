namespace MDRAVA.API.Models.Forwarding;

public enum ProxyTimeoutKind
{
    ClientRequestHead,
    ClientKeepAliveIdle,
    ClientRequestBodyIdle,
    UpstreamConnect,
    UpstreamResponseHead,
    UpstreamResponseBodyIdle,
    DownstreamWrite,
    TlsHandshake,
    TunnelIdle
}
