using MDRAVA.BLL.Http;
using MDRAVA.BLL.ControlPlane.Headers;
namespace MDRAVA.BLL.ControlPlane.Http1;

public sealed class Http1ResponseHead
{
    public Http1ResponseHead(
        string version,
        int statusCode,
        string reasonPhrase,
        Http1ResponseFraming framing,
        IReadOnlyList<ProxyHeaderField> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        Version = version;
        StatusCode = statusCode;
        ReasonPhrase = reasonPhrase;
        Framing = framing;
        Headers = headers.ToArray();
    }

    public string Version { get; }

    public int StatusCode { get; }

    public string ReasonPhrase { get; }

    public Http1ResponseFraming Framing { get; }

    public IReadOnlyList<ProxyHeaderField> Headers { get; }
}
