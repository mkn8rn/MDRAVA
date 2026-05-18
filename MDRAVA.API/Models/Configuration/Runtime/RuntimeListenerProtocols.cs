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
    public static readonly IReadOnlyList<string> SupportedConfigValues =
    [
        "http1",
        "http2",
        "http1AndHttp2",
        "http3Preview",
        "http1AndHttp3Preview",
        "http2AndHttp3Preview",
        "http1AndHttp2AndHttp3Preview"
    ];

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
        return protocols.HasHttp3();
    }

    public static bool TryParseConfigText(string? protocols, out RuntimeListenerProtocols parsed)
    {
        parsed = string.IsNullOrWhiteSpace(protocols)
            ? RuntimeListenerProtocols.Http1
            : protocols.Trim().ToLowerInvariant() switch
            {
                "http2" => RuntimeListenerProtocols.Http2,
                "http1andhttp2" => RuntimeListenerProtocols.Http1AndHttp2,
                "http3preview" => RuntimeListenerProtocols.Http3Preview,
                "http1andhttp3preview" => RuntimeListenerProtocols.Http1AndHttp3Preview,
                "http2andhttp3preview" => RuntimeListenerProtocols.Http2AndHttp3Preview,
                "http1andhttp2andhttp3preview" => RuntimeListenerProtocols.Http1AndHttp2AndHttp3Preview,
                "http1" => RuntimeListenerProtocols.Http1,
                _ => RuntimeListenerProtocols.None
            };
        return parsed != RuntimeListenerProtocols.None;
    }

    public static RuntimeListenerProtocols ParseConfigTextOrDefault(string? protocols)
    {
        return TryParseConfigText(protocols, out var parsed)
            ? parsed
            : RuntimeListenerProtocols.Http1;
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
