namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record ProxyConfigurationSnapshot(
    int Version,
    DateTimeOffset LoadedAtUtc,
    string SourceDirectory,
    IReadOnlyList<string> SourceFiles,
    ProxyConfigurationDiscovery Discovery,
    RuntimeAdminSecurityOptions AdminSecurity,
    RuntimeAcmeOptions Acme,
    RuntimeTimeouts Timeouts,
    RuntimeConnectionLimits ConnectionLimits,
    RuntimeObservabilityOptions Observability,
    RuntimeLimits Limits,
    RuntimeForwardedHeadersOptions ForwardedHeaders,
    IReadOnlyDictionary<string, RuntimeCertificate> Certificates,
    IReadOnlyList<RuntimeListener> Listeners,
    IReadOnlyList<RuntimeRoute> Routes)
{
    public bool TryGetFirstEnabledListener(out RuntimeListener? listener)
    {
        listener = Listeners.FirstOrDefault(static candidate => candidate.Enabled);
        return listener is not null;
    }

    public RuntimeListener GetFirstEnabledListener()
    {
        return Listeners.First(static listener => listener.Enabled);
    }
}
