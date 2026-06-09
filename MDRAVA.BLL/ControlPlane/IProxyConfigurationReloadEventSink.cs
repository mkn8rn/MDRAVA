namespace MDRAVA.BLL.ControlPlane;

public interface IProxyConfigurationReloadEventSink
{
    void LoadFailed(string sourceDirectory, IReadOnlyList<string> errors);

    void Loaded(int version, string sourceDirectory);
}
