using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

internal sealed class ProxyRouteDiagnosticsRuntimeListener
    : IProxyRouteDiagnosticsListener
{
    public ProxyRouteDiagnosticsRuntimeListener(RuntimeListener runtimeListener)
    {
        Name = runtimeListener.Name;
        Transport = runtimeListener.Transport == RuntimeListenerTransport.Https ? "https" : "http";
        Address = runtimeListener.Address;
        Port = runtimeListener.Port;
        Enabled = runtimeListener.Enabled;
        Protocols = runtimeListener.Protocols;
        Http3EnabledForTraffic = runtimeListener.Http3.EnabledForTraffic;
    }

    public string Name { get; }

    public string Transport { get; }

    public string Address { get; }

    public int Port { get; }

    public bool Enabled { get; }

    public RuntimeListenerProtocols Protocols { get; }

    public bool Http3EnabledForTraffic { get; }
}
