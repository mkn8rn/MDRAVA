namespace MDRAVA.API.Models.Configuration.Runtime;

[Flags]
public enum RuntimeListenerProtocols
{
    None = 0,
    Http1 = 1,
    Http2 = 2,
    Http1AndHttp2 = Http1 | Http2
}
