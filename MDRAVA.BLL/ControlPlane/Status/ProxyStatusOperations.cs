namespace MDRAVA.BLL.ControlPlane.Status;

public sealed class ProxyStatusOperations : IProxyStatusOperations
{
    private readonly IProxyStatusInputReader _inputReader;

    public ProxyStatusOperations(IProxyStatusInputReader inputReader)
    {
        _inputReader = inputReader;
    }

    public ProxyStatus GetStatus()
    {
        return ProxyStatusResponseBuilder.Build(_inputReader.Read());
    }
}
