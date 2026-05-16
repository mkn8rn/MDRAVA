namespace MDRAVA.API.Models.Configuration;

public sealed class ProxyOperationalOptions
{
    public ProxyAdminOptions Admin { get; init; } = new();

    public ProxyTimeoutOptions Timeouts { get; init; } = new();

    public ProxyConnectionOptions Connections { get; init; } = new();

    public ProxyObservabilityOptions Observability { get; init; } = new();

    public ProxyLimitsOptions Limits { get; init; } = new();

    public ProxyForwardedHeadersOptions ForwardedHeaders { get; init; } = new();

    public List<CertificateOptions> Certificates { get; init; } = [];
}
