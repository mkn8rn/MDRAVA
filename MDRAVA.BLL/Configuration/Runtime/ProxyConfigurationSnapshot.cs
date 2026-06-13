namespace MDRAVA.BLL.Configuration;

public sealed record ProxyConfigurationSnapshot
{
    public ProxyConfigurationSnapshot(
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
        this.Version = Version;
        this.LoadedAtUtc = LoadedAtUtc;
        this.SourceDirectory = SourceDirectory;
        this.SourceFiles = RuntimeList.Copy(SourceFiles);
        this.Discovery = Discovery;
        this.AdminSecurity = AdminSecurity;
        this.Acme = Acme;
        this.Timeouts = Timeouts;
        this.ConnectionLimits = ConnectionLimits;
        this.Observability = Observability;
        this.Limits = Limits;
        this.ForwardedHeaders = ForwardedHeaders;
        this.Certificates = RuntimeList.CopyDictionary(Certificates, StringComparer.OrdinalIgnoreCase);
        this.Listeners = RuntimeList.Copy(Listeners);
        this.Routes = RuntimeList.Copy(Routes);
    }

    public int Version { get; init; }

    public DateTimeOffset LoadedAtUtc { get; init; }

    public string SourceDirectory { get; init; }

    public IReadOnlyList<string> SourceFiles { get; }

    public ProxyConfigurationDiscovery Discovery { get; init; }

    public RuntimeAdminSecurityOptions AdminSecurity { get; init; }

    public RuntimeAcmeOptions Acme { get; init; }

    public RuntimeTimeouts Timeouts { get; init; }

    public RuntimeConnectionLimits ConnectionLimits { get; init; }

    public RuntimeObservabilityOptions Observability { get; init; }

    public RuntimeLimits Limits { get; init; }

    public RuntimeForwardedHeadersOptions ForwardedHeaders { get; init; }

    public IReadOnlyDictionary<string, RuntimeCertificate> Certificates { get; }

    public IReadOnlyList<RuntimeListener> Listeners { get; }

    public IReadOnlyList<RuntimeRoute> Routes { get; }

    public RuntimeMetricsOptions Metrics { get; init; } = RuntimeMetricsOptions.Default;

    public ProxyConfigurationSnapshot WithCertificates(
        IReadOnlyDictionary<string, RuntimeCertificate> certificates)
    {
        return new ProxyConfigurationSnapshot(
            Version,
            LoadedAtUtc,
            SourceDirectory,
            SourceFiles,
            Discovery,
            AdminSecurity,
            Acme,
            Timeouts,
            ConnectionLimits,
            Observability,
            Limits,
            ForwardedHeaders,
            certificates,
            Listeners,
            Routes)
        {
            Metrics = Metrics
        };
    }

    public ProxyConfigurationSnapshot WithListenersAndRoutes(
        IReadOnlyList<RuntimeListener> listeners,
        IReadOnlyList<RuntimeRoute> routes)
    {
        return new ProxyConfigurationSnapshot(
            Version,
            LoadedAtUtc,
            SourceDirectory,
            SourceFiles,
            Discovery,
            AdminSecurity,
            Acme,
            Timeouts,
            ConnectionLimits,
            Observability,
            Limits,
            ForwardedHeaders,
            Certificates,
            listeners,
            routes)
        {
            Metrics = Metrics
        };
    }
}
