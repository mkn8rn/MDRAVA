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
        if (!RuntimeHttp3AltSvcPolicy.IsEnabled(listener))
        {
            _metrics.Http3AltSvcSuppressed();
            return false;
        }

        var runtime = _runtimeState.Snapshot();
        if (!RuntimeHttp3AltSvcPolicy.HasActiveQuicListener(listener, runtime.Listeners))
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
}
