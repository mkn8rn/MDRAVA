namespace MDRAVA.API.Proxy.Connections;

public sealed class UpstreamTlsException : IOException
{
    public UpstreamTlsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
