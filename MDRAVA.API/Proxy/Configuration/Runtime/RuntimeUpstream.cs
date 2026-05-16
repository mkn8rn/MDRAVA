namespace MDRAVA.API.Proxy.Configuration.Runtime;

public sealed record RuntimeUpstream(
    string Name,
    string Address,
    int Port);
