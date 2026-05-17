namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeQuicListenerIdentity(
    string Name,
    string Address,
    int Port,
    bool TlsEnabled)
{
    public string Key => $"{Normalize(Name)}|quic";

    public string BindKey => $"{Normalize(Address)}|{Port}|udp|quic";

    public static RuntimeQuicListenerIdentity From(RuntimeListener listener)
    {
        return new RuntimeQuicListenerIdentity(
            listener.Name,
            listener.Address,
            listener.Port,
            listener.Transport == RuntimeListenerTransport.Https);
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }
}
