using MDRAVA.API.Proxy.Hosting;
using MDRAVA.API.Proxy.Protocol;

namespace MDRAVA.API.Proxy.Http3;

public sealed class Http3AltSvcPolicy
{
    private readonly ProxyRuntimeState _runtimeState;

    public Http3AltSvcPolicy(ProxyRuntimeState runtimeState)
    {
        _runtimeState = runtimeState;
    }

    public bool TryCreateHeader(RuntimeListener listener, out Http1HeaderField header)
    {
        header = null!;
        if (!listener.Http3AltSvc.Enabled || !listener.Http3.EnabledForTraffic)
        {
            return false;
        }

        var runtime = _runtimeState.Snapshot();
        if (!HasActiveQuicListener(listener, runtime.Listeners))
        {
            return false;
        }

        header = new Http1HeaderField(
            "Alt-Svc",
            $"h3=\":{listener.Port}\"; ma={listener.Http3AltSvc.MaxAgeSeconds}");
        return true;
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
