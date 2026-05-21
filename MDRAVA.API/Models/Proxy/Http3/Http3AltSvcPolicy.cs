using MDRAVA.API.Proxy.Hosting;
using MDRAVA.API.Proxy.Metrics;
using MDRAVA.API.Proxy.Protocol;

namespace MDRAVA.API.Proxy.Http3;

public sealed class Http3AltSvcPolicy
{
    private readonly ProxyRuntimeState _runtimeState;
    private readonly ProxyMetrics _metrics;

    public Http3AltSvcPolicy(ProxyRuntimeState runtimeState, ProxyMetrics metrics)
    {
        _runtimeState = runtimeState;
        _metrics = metrics;
    }

    public bool TryCreateHeader(RuntimeListener listener, out Http1HeaderField header)
    {
        header = null!;
        if (!IsEnabled(listener))
        {
            _metrics.Http3AltSvcSuppressed();
            return false;
        }

        var runtime = _runtimeState.Snapshot();
        if (!HasActiveQuicListener(listener, runtime.Listeners))
        {
            _metrics.Http3AltSvcSuppressed();
            return false;
        }

        header = new Http1HeaderField(
            "Alt-Svc",
            $"h3=\":{listener.Port}\"; ma={listener.Http3AltSvc.MaxAgeSeconds}");
        _metrics.Http3AltSvcEmitted();
        return true;
    }

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
