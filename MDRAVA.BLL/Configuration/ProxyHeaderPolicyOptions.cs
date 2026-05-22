namespace MDRAVA.BLL.Configuration;

public sealed class ProxyHeaderPolicyOptions
{
    public List<ProxyHeaderSetOptions> SetRequestHeaders { get; init; } = [];

    public List<string> RemoveRequestHeaders { get; init; } = [];

    public List<ProxyHeaderSetOptions> SetResponseHeaders { get; init; } = [];

    public List<string> RemoveResponseHeaders { get; init; } = [];
}
