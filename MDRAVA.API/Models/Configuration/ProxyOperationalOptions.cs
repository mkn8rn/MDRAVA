namespace MDRAVA.API.Models.Configuration;

public sealed class ProxyOperationalOptions
{
    public ProxyTimeoutOptions Timeouts { get; init; } = new();

    public ProxyConnectionOptions Connections { get; init; } = new();

    public ProxyObservabilityOptions Observability { get; init; } = new();

    public ProxyLimitsOptions Limits { get; init; } = new();

    public List<CertificateOptions> Certificates { get; init; } = [];
}
