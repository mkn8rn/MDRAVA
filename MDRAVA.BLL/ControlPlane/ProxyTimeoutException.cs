namespace MDRAVA.BLL.ControlPlane;

public sealed class ProxyTimeoutException : TimeoutException
{
    public ProxyTimeoutException(ProxyTimeoutKind kind, TimeSpan timeout)
        : base($"{kind} timed out after {timeout}.")
    {
        Kind = kind;
        Timeout = timeout;
    }

    public ProxyTimeoutKind Kind { get; }

    public TimeSpan Timeout { get; }
}
