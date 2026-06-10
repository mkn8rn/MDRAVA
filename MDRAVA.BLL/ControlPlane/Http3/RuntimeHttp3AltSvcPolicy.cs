using MDRAVA.BLL.ControlPlane;
using MDRAVA.BLL.ControlPlane.Listeners;
using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Http3;

public static class RuntimeHttp3AltSvcPolicy
{
    public static bool IsEnabled(RuntimeListener listener)
    {
        return listener.Http3.EnabledForTraffic
            && (listener.Http3AltSvc.Enabled
                || string.Equals(listener.Http3.EnablementLevel, "default", StringComparison.OrdinalIgnoreCase));
    }

    public static bool HasActiveQuicListener(
        RuntimeListener listener,
        IReadOnlyList<ProxyListenerStatus> runtimeListeners)
    {
        var identity = listener.QuicIdentity;
        return identity is not null
            && runtimeListeners.Any(candidate =>
                string.Equals(candidate.Kind, "quic", StringComparison.OrdinalIgnoreCase)
                && candidate.State == ProxyListenerState.Active
                && string.Equals(candidate.Identity, identity.Key, StringComparison.OrdinalIgnoreCase));
    }
}
