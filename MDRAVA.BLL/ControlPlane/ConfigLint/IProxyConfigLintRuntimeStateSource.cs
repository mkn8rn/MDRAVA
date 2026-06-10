using MDRAVA.BLL.ControlPlane.Listeners;

namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public interface IProxyConfigLintRuntimeStateSource
{
    IReadOnlyList<ProxyListenerStatus> GetListeners();
}
