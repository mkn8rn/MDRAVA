using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Http3;

public static class ProxyHttp3RequestTranslationRuntimeMapper
{
    public static Http3RequestTranslationListenerInput ToListenerInput(RuntimeListener listener)
    {
        return new Http3RequestTranslationListenerInput(
            listener.Transport == RuntimeListenerTransport.Https);
    }
}
