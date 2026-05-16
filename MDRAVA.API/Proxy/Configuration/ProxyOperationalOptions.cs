namespace MDRAVA.API.Proxy.Configuration;

public sealed class ProxyOperationalOptions
{
    public ProxyTimeoutOptions Timeouts { get; init; } = new();

    public List<CertificateOptions> Certificates { get; init; } = [];
}
