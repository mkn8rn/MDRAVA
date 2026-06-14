using MDRAVA.BLL.ControlPlane.Listeners;

namespace MDRAVA.BLL.ControlPlane.Http3;

public static class Http3AltSvcListenerPolicy
{
    public static bool IsEnabled(Http3AltSvcListenerInput listener)
    {
        return listener.EnabledForTraffic
            && (listener.AltSvcEnabled
                || string.Equals(listener.EnablementLevel, "default", StringComparison.OrdinalIgnoreCase));
    }

    public static bool HasActiveQuicListener(
        Http3AltSvcListenerInput listener,
        IReadOnlyList<ProxyListenerStatus> runtimeListeners)
    {
        return !string.IsNullOrWhiteSpace(listener.QuicListenerIdentity)
            && runtimeListeners.Any(candidate =>
                string.Equals(candidate.Kind, "quic", StringComparison.OrdinalIgnoreCase)
                && candidate.State == ProxyListenerState.Active
                && string.Equals(candidate.Identity, listener.QuicListenerIdentity, StringComparison.OrdinalIgnoreCase));
    }
}
