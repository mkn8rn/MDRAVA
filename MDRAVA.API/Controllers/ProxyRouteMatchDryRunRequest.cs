using BusinessRouteMatchDryRunRequest = MDRAVA.BLL.ControlPlane.RouteDiagnostics.RouteMatchDryRunRequest;

namespace MDRAVA.API.Controllers;

public sealed record ProxyRouteMatchDryRunRequest(
    string Scheme,
    string Host,
    int? Port,
    string Method,
    string Path,
    string Query,
    IReadOnlyDictionary<string, string?>? Headers,
    string? ClientIp,
    string? ListenerName,
    string? Protocol = null)
{
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
}
