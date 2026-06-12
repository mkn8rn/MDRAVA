using MDRAVA.BLL.Http;
using MDRAVA.BLL.ControlPlane.Headers;
using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Http3;

public sealed class Http3AltSvcPolicy
{
    private readonly IHttp3AltSvcRuntimeListenerSource _runtimeListeners;
    private readonly IProxyHttp3AltSvcMetricsSink _metrics;

    public Http3AltSvcPolicy(
        IHttp3AltSvcRuntimeListenerSource runtimeListeners,
        IProxyHttp3AltSvcMetricsSink metrics)
    {
        _runtimeListeners = runtimeListeners;
        _metrics = metrics;
    }

    public Http3AltSvcHeaderResult CreateHeader(RuntimeListener listener)
    {
        if (!RuntimeHttp3AltSvcPolicy.IsEnabled(listener))
        {
            _metrics.Http3AltSvcSuppressed();
            return Http3AltSvcHeaderResult.Suppressed;
        }

        if (!RuntimeHttp3AltSvcPolicy.HasActiveQuicListener(
            listener,
            _runtimeListeners.ReadRuntimeListeners()))
        {
            _metrics.Http3AltSvcSuppressed();
            return Http3AltSvcHeaderResult.Suppressed;
        }

        _metrics.Http3AltSvcEmitted();
        return Http3AltSvcHeaderResult.Emitted(
            new ProxyHeaderField(
                "Alt-Svc",
                $"h3=\":{listener.Port}\"; ma={listener.Http3AltSvc.MaxAgeSeconds}"));
    }
}
