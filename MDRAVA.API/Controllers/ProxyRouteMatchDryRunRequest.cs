using System.Collections.ObjectModel;

using BusinessRouteMatchDryRunRequest = MDRAVA.BLL.ControlPlane.RouteDiagnostics.RouteMatchDryRunRequest;

namespace MDRAVA.API.Controllers;

public sealed record ProxyRouteMatchDryRunRequest
{
    public ProxyRouteMatchDryRunRequest(
        string scheme,
        string host,
        int? port,
        string method,
        string path,
        string query,
        IReadOnlyDictionary<string, string?>? headers,
        string? clientIp,
        string? listenerName,
        string? protocol = null)
    {
        Scheme = scheme;
        Host = host;
        Port = port;
        Method = method;
        Path = path;
        Query = query;
        Headers = CopyHeaders(headers);
        ClientIp = clientIp;
        ListenerName = listenerName;
        Protocol = protocol;
    }

    public string Scheme { get; }

    public string Host { get; }

    public int? Port { get; }

    public string Method { get; }

    public string Path { get; }

    public string Query { get; }

    public IReadOnlyDictionary<string, string?>? Headers { get; }

    public string? ClientIp { get; }

    public string? ListenerName { get; }

    public string? Protocol { get; }

    public BusinessRouteMatchDryRunRequest ToRouteMatchDryRunRequest()
    {
        return new BusinessRouteMatchDryRunRequest(
            Scheme,
            Host,
            Port,
            Method,
            Path,
            Query,
            Headers ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            ClientIp,
            ListenerName,
            Protocol);
    }

    private static IReadOnlyDictionary<string, string?>? CopyHeaders(
        IReadOnlyDictionary<string, string?>? headers)
    {
        return headers is null
            ? null
            : new ReadOnlyDictionary<string, string?>(
                new Dictionary<string, string?>(headers, StringComparer.OrdinalIgnoreCase));
    }
}
