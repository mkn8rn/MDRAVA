namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed record ProxyRouteDiagnosticsRequestFraming(
    ProxyRouteDiagnosticsBodyKind Kind,
    long? ContentLength)
{
    public static ProxyRouteDiagnosticsRequestFraming None { get; } = new(ProxyRouteDiagnosticsBodyKind.None, null);

    public static ProxyRouteDiagnosticsRequestFraming Chunked { get; } = new(ProxyRouteDiagnosticsBodyKind.Chunked, null);

    public static ProxyRouteDiagnosticsRequestFraming FromContentLength(long contentLength)
    {
        return contentLength == 0
            ? None
            : new ProxyRouteDiagnosticsRequestFraming(ProxyRouteDiagnosticsBodyKind.ContentLength, contentLength);
    }
}
