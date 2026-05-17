namespace MDRAVA.API.Models.Configuration.Runtime;

public static class RuntimeUpstreamProtocol
{
    public const string Http1 = "http1";

    public const string Http2 = "http2";

    public static bool IsHttp2(string protocol)
    {
        return string.Equals(protocol, Http2, StringComparison.OrdinalIgnoreCase);
    }
}
