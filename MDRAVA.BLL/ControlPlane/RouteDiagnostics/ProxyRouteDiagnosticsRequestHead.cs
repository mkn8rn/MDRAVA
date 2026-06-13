using MDRAVA.BLL.Http;

namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed class ProxyRouteDiagnosticsRequestHead
{
    public ProxyRouteDiagnosticsRequestHead(
        string method,
        string target,
        string path,
        string version,
        string host,
        ProxyRouteDiagnosticsRequestFraming framing,
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

    public ProxyRouteDiagnosticsRequestFraming Framing { get; }

    public IReadOnlyList<ProxyHeaderField> Headers { get; }
}
