namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public interface IProxyConfigLintRuntimeStateSource
{
    IReadOnlyList<ProxyConfigLintRuntimeListenerState> GetListenerStates();
}

public sealed record ProxyConfigLintRuntimeListenerState(
    string Identity,
    string Kind,
    bool Active);
