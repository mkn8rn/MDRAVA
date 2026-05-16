namespace MDRAVA.API.Proxy.Protocol;

public sealed class Http1ResponseHead
{
    public Http1ResponseHead(
        string version,
        int statusCode,
        string reasonPhrase,
        Http1ResponseFraming framing,
        IReadOnlyList<Http1HeaderField> headers)
    {
        Version = version;
        StatusCode = statusCode;
        ReasonPhrase = reasonPhrase;
        Framing = framing;
        Headers = headers;
    }

    public string Version { get; }

    public int StatusCode { get; }

    public string ReasonPhrase { get; }

    public Http1ResponseFraming Framing { get; }

    public IReadOnlyList<Http1HeaderField> Headers { get; }
}
