using MDRAVA.BLL.ControlPlane.Headers;
namespace MDRAVA.BLL.ControlPlane.Http1;

public sealed class Http1RequestHead
{
    public Http1RequestHead(
        string method,
        string target,
        string path,
        string version,
        string host,
        Http1RequestFraming framing,
        IReadOnlyList<ProxyHeaderField> headers)
    {
        Method = method;
        Target = target;
        Path = path;
        Version = version;
        Host = host;
        Framing = framing;
        Headers = headers;
    }

    public string Method { get; }

    public string Target { get; }

    public string Path { get; }

    public string Version { get; }

    public string Host { get; }

    public Http1RequestFraming Framing { get; }

    public IReadOnlyList<ProxyHeaderField> Headers { get; }

    public long? ContentLength => Framing.ContentLength;

    public bool HasTransferEncoding => Framing.Kind == Http1BodyKind.Chunked;
}
