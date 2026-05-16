namespace MDRAVA.API.Proxy.Forwarding;

public enum ProxyTimeoutKind
{
    ClientRequestHead,
    ClientRequestBodyIdle,
    UpstreamConnect,
    UpstreamResponseHead,
    UpstreamResponseBodyIdle,
    DownstreamWrite
}
