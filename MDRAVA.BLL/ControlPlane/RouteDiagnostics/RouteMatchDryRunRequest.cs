using System.Collections.ObjectModel;

namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed record RouteMatchDryRunRequest
{
    public RouteMatchDryRunRequest(
        string Scheme,
        string Host,
        int? Port,
        string Method,
        string Path,
        string Query,
        IReadOnlyDictionary<string, string?> Headers,
        string? ClientIp,
        string? ListenerName,
        string? Protocol = null)
    {
        ArgumentNullException.ThrowIfNull(Headers);

        this.Scheme = Scheme;
        this.Host = Host;
        this.Port = Port;
        this.Method = Method;
        this.Path = Path;
        this.Query = Query;
        this.Headers = new ReadOnlyDictionary<string, string?>(new Dictionary<string, string?>(Headers));
        this.ClientIp = ClientIp;
        this.ListenerName = ListenerName;
        this.Protocol = Protocol;
    }

    public string Scheme { get; init; }

    public string Host { get; init; }

    public int? Port { get; init; }

    public string Method { get; init; }

    public string Path { get; init; }

    public string Query { get; init; }

    public IReadOnlyDictionary<string, string?> Headers { get; }

    public string? ClientIp { get; init; }

    public string? ListenerName { get; init; }

    public string? Protocol { get; init; }
}
