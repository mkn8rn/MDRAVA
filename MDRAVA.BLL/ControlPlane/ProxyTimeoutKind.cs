namespace MDRAVA.BLL.ControlPlane;

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
