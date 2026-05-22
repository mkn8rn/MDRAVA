namespace MDRAVA.BLL.ControlPlane;

public sealed class ProxyStatusAdministrationService
{
    private readonly IProxyStatusOperations _operations;

    public ProxyStatusAdministrationService(IProxyStatusOperations operations)
    {
        _operations = operations;
    }

    public ProxyStatusResponse GetStatus()
    {
        return _operations.GetStatus();
    }
}
