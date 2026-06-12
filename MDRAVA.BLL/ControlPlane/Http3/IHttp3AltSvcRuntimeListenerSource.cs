using MDRAVA.BLL.ControlPlane.Listeners;

namespace MDRAVA.BLL.ControlPlane.Http3;

public interface IHttp3AltSvcRuntimeListenerSource
{
    IReadOnlyList<ProxyListenerStatus> ReadRuntimeListeners();
}
