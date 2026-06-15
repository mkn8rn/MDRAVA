using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Headers;

public static class ProxyForwardedHeadersRuntimeMapper
{
    public static ForwardedHeadersListener ToListener(RuntimeListener listener)
    {
        return new ForwardedHeadersListener(
            listener.Transport == RuntimeListenerTransport.Https ? "https" : "http",
            listener.Port);
    }
}
