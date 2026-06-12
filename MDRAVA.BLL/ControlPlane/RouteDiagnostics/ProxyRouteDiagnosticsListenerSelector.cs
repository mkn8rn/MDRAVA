using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public static class ProxyRouteDiagnosticsListenerSelector
{
    public static IProxyRouteDiagnosticsListener? Select(
        IProxyRouteDiagnosticsConfigurationSnapshot snapshot,
        string? listenerName,
        string scheme,
        int? port,
        string? protocol)
    {
        var transport = scheme == "https" ? "https" : "http";
        IEnumerable<IProxyRouteDiagnosticsListener> listeners = snapshot.Listeners.Where(static listener => listener.Enabled);
        if (!string.IsNullOrWhiteSpace(listenerName))
        {
            listeners = listeners.Where(listener => string.Equals(listener.Name, listenerName, StringComparison.OrdinalIgnoreCase));
        }

        listeners = listeners.Where(listener => string.Equals(listener.Transport, transport, StringComparison.OrdinalIgnoreCase));
        if (port.HasValue)
        {
            listeners = listeners.Where(listener => listener.Port == port.Value);
        }

        listeners = protocol switch
        {
            "http1" => listeners.Where(static listener => listener.Protocols.HasFlag(RuntimeListenerProtocols.Http1)),
            "http2" => listeners.Where(static listener => listener.Protocols.HasFlag(RuntimeListenerProtocols.Http2)),
            "http3" => listeners.Where(static listener => listener.Http3EnabledForTraffic),
            _ => listeners
        };

        return listeners.FirstOrDefault();
    }
}
