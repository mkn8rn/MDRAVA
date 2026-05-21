namespace MDRAVA.API.Models.Configuration.Runtime;

[Flags]
public enum RuntimeListenerProtocols
{
    None = 0,
    Http1 = 1,
    Http2 = 2,
    // Compatibility name for existing config files. Runtime/status code should
    // use HasHttp3 when describing stable HTTP/3 behavior.
    Http3Preview = 4,
    Http1AndHttp2 = Http1 | Http2,
    Http1AndHttp3Preview = Http1 | Http3Preview,
    Http2AndHttp3Preview = Http2 | Http3Preview,
    Http1AndHttp2AndHttp3Preview = Http1 | Http2 | Http3Preview
}

public static class RuntimeListenerProtocolExtensions
{
    public static readonly IReadOnlyList<string> SupportedConfigValues = RuntimeHttp3Compatibility.SupportedProtocolConfigValues;

    public static bool HasTcpProtocols(this RuntimeListenerProtocols protocols)
    {
        return (protocols & (RuntimeListenerProtocols.Http1 | RuntimeListenerProtocols.Http2)) != 0;
    }

    public static bool HasHttp3(this RuntimeListenerProtocols protocols)
    {
        return protocols.HasFlag(RuntimeListenerProtocols.Http3Preview);
    }

    public static bool HasHttp3Preview(this RuntimeListenerProtocols protocols)
    {
        // Legacy alias retained for old call sites and compatibility tests.
        return protocols.HasHttp3();
    }

    public static bool TryParseConfigText(string? protocols, out RuntimeListenerProtocols parsed)
    {
        return RuntimeHttp3Compatibility.TryParseProtocols(protocols, out parsed);
    }

    public static RuntimeListenerProtocols ParseConfigTextOrDefault(string? protocols)
    {
        return RuntimeHttp3Compatibility.ParseProtocolsOrDefault(protocols);
    }

    public static string ToConfigText(this RuntimeListenerProtocols protocols)
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
