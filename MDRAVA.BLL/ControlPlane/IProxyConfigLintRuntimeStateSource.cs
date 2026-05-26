namespace MDRAVA.BLL.ControlPlane;

public interface IProxyConfigLintRuntimeStateSource
{
    IReadOnlyList<ProxyListenerStatus> GetListeners();
}
