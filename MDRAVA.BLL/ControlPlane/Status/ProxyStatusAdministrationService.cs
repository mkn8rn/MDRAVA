namespace MDRAVA.BLL.ControlPlane.Status;

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
