namespace MDRAVA.BLL.Configuration;

public sealed class ProxyForwardedHeadersOptions
{
    public bool Enabled { get; init; } = true;

    public List<string> TrustedProxies { get; init; } = [];
}
