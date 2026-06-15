namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeListenerIdentity(
    string Name,
    string Address,
    int Port,
    RuntimeListenerTransport Transport,
    bool TlsEnabled)
{
    public string Key => Normalize(Name);

    public string BindKey => $"{Normalize(Address)}|{Port}|{RuntimeListenerTransportScheme.FromTransport(Transport)}";

    public static RuntimeListenerIdentity From(RuntimeListener listener)
    {
        return new RuntimeListenerIdentity(
            listener.Name,
            listener.Address,
            listener.Port,
            listener.Transport,
            listener.Transport == RuntimeListenerTransport.Https);
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }
}
