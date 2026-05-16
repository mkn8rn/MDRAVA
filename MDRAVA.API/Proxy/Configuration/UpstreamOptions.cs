namespace MDRAVA.API.Proxy.Configuration;

public sealed class UpstreamOptions
{
    public string Name { get; init; } = "";

    public string Address { get; init; } = "";

    public int Port { get; init; }
}
