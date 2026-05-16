namespace MDRAVA.API.Models.Configuration;

public sealed class UpstreamOptions
{
    public string Name { get; init; } = "";

    public string Address { get; init; } = "";

    public int Port { get; init; }

    public int Weight { get; init; } = 1;
}
