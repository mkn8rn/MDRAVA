namespace MDRAVA.BLL.Configuration;

public enum RuntimeListenerTransport
{
    Http,
    Https
}

public static class RuntimeListenerTransportScheme
{
    public static string FromTransport(RuntimeListenerTransport transport)
    {
        return transport switch
        {
            RuntimeListenerTransport.Http => "http",
            RuntimeListenerTransport.Https => "https",
            _ => throw new ArgumentOutOfRangeException(nameof(transport), transport, null)
        };
    }
}
