using MDRAVA.BLL.ControlPlane.Headers;
using MDRAVA.BLL.ControlPlane.Status;
using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Http3;

public sealed class Http3AltSvcPolicy
{
    private readonly IProxyStatusRuntimeStateSource _runtimeState;
    private readonly IProxyHttp3AltSvcMetricsSink _metrics;

    public Http3AltSvcPolicy(
        IProxyStatusRuntimeStateSource runtimeState,
        IProxyHttp3AltSvcMetricsSink metrics)
    {
        _runtimeState = runtimeState;
        _metrics = metrics;
    }

    public bool TryCreateHeader(RuntimeListener listener, out ProxyHeaderField header)
    {
        header = null!;
        if (!RuntimeHttp3AltSvcPolicy.IsEnabled(listener))
        {
            _metrics.Http3AltSvcSuppressed();
            return false;
        }

        var runtime = _runtimeState.ReadRuntime();
        if (!RuntimeHttp3AltSvcPolicy.HasActiveQuicListener(listener, runtime.Listeners))
        {
            _metrics.Http3AltSvcSuppressed();
            return false;
        }

        header = new ProxyHeaderField(
            "Alt-Svc",
            $"h3=\":{listener.Port}\"; ma={listener.Http3AltSvc.MaxAgeSeconds}");
        _metrics.Http3AltSvcEmitted();
        return true;
    }
}
