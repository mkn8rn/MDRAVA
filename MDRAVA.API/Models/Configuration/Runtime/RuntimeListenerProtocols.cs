namespace MDRAVA.API.Models.Configuration.Runtime;

[Flags]
public enum RuntimeListenerProtocols
{
    None = 0,
    Http1 = 1,
    Http2 = 2,
    Http3Preview = 4,
    Http1AndHttp2 = Http1 | Http2,
    Http1AndHttp3Preview = Http1 | Http3Preview,
    Http2AndHttp3Preview = Http2 | Http3Preview,
    Http1AndHttp2AndHttp3Preview = Http1 | Http2 | Http3Preview
}

public static class RuntimeListenerProtocolExtensions
{
    public static bool HasTcpProtocols(this RuntimeListenerProtocols protocols)
    {
        return (protocols & (RuntimeListenerProtocols.Http1 | RuntimeListenerProtocols.Http2)) != 0;
    }

    public static bool HasHttp3Preview(this RuntimeListenerProtocols protocols)
    {
        return protocols.HasFlag(RuntimeListenerProtocols.Http3Preview);
    }
}
