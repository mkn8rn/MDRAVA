namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record ProxyConfigurationProjection(
    int Version,
    DateTimeOffset LoadedAtUtc,
    string SourceDirectory,
    IReadOnlyList<string> SourceFiles,
    RuntimeTimeouts Timeouts,
    RuntimeConnectionLimits ConnectionLimits,
    RuntimeObservabilityOptions Observability,
    RuntimeLimits Limits,
    RuntimeForwardedHeadersOptions ForwardedHeaders,
    IReadOnlyList<RuntimeCertificateProjection> Certificates,
    IReadOnlyList<RuntimeListener> Listeners,
    IReadOnlyList<RuntimeRoute> Routes);
