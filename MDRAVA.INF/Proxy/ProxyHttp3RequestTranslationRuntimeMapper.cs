using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Http3;

namespace MDRAVA.INF.Proxy;

internal static class ProxyHttp3RequestTranslationRuntimeMapper
{
    public static Http3RequestTranslationListenerInput ToListenerInput(RuntimeListener listener)
    {
        return new Http3RequestTranslationListenerInput(
            listener.Transport == RuntimeListenerTransport.Https);
    }
}
