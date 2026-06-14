using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Http3;

namespace MDRAVA.INF.Proxy;

internal static class ProxyHttp3AltSvcRuntimeMapper
{
    public static Http3AltSvcListenerInput ToListenerInput(RuntimeListener listener)
    {
        return new Http3AltSvcListenerInput(
            listener.Http3.EnabledForTraffic,
            listener.Http3.EnablementLevel,
            listener.Http3AltSvc.Enabled,
            listener.Http3AltSvc.MaxAgeSeconds,
            listener.Port,
            listener.QuicIdentity?.Key);
    }
}
