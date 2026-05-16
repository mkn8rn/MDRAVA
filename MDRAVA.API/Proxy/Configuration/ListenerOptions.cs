namespace MDRAVA.API.Proxy.Configuration;

public sealed class ListenerOptions
{
    public string Name { get; init; } = "";

    public string Address { get; init; } = "0.0.0.0";

    public int Port { get; init; } = 8080;

    public bool Enabled { get; init; } = true;

    public int Backlog { get; init; } = 512;

    public int MaxRequestHeadBytes { get; init; } = 32 * 1024;

    public int MaxResponseHeadBytes { get; init; } = 32 * 1024;

    public int MaxChunkLineBytes { get; init; } = 1024;

    public int ForwardingBufferBytes { get; init; } = 64 * 1024;
}
