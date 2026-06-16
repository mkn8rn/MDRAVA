using BusinessProxyConfigurationProjection =
    MDRAVA.BLL.ControlPlane.ConfigurationManagement.ProxyConfigurationProjection;

namespace MDRAVA.API.Controllers;

public sealed record ProxyConfigurationResponse
{
    public ProxyConfigurationResponse(
        int version,
        DateTimeOffset loadedAtUtc,
        string sourceDirectory,
        IReadOnlyList<string> sourceFiles,
        ProxyConfigurationDiscoveryResponse discovery,
        RuntimeAdminSecurityResponse adminSecurity,
        RuntimeAcmeResponse acme,
        RuntimeTimeoutsResponse timeouts,
        RuntimeConnectionLimitsResponse connectionLimits,
        RuntimeObservabilityResponse observability,
        RuntimeLimitsResponse limits,
        RuntimeForwardedHeadersResponse forwardedHeaders,
        RuntimeMetricsResponse metrics,
        RuntimeHttp3SupportResponse http3,
        IReadOnlyList<RuntimeCertificateResponse> certificates,
        IReadOnlyList<RuntimeListenerResponse> listeners,
        IReadOnlyList<RuntimeRouteResponse> routes)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(adminSecurity);
        ArgumentNullException.ThrowIfNull(acme);
        ArgumentNullException.ThrowIfNull(timeouts);
        ArgumentNullException.ThrowIfNull(connectionLimits);
        ArgumentNullException.ThrowIfNull(observability);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(forwardedHeaders);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(http3);

        Version = version;
        LoadedAtUtc = loadedAtUtc;
        SourceDirectory = sourceDirectory;
        SourceFiles = ApiResponseList.Copy(sourceFiles);
        Discovery = discovery;
        AdminSecurity = adminSecurity;
        Acme = acme;
        Timeouts = timeouts;
        ConnectionLimits = connectionLimits;
        Observability = observability;
        Limits = limits;
        ForwardedHeaders = forwardedHeaders;
        Metrics = metrics;
        Http3 = http3;
        Certificates = ApiResponseList.Copy(certificates);
        Listeners = ApiResponseList.Copy(listeners);
        Routes = ApiResponseList.Copy(routes);
    }

    public int Version { get; }

    public DateTimeOffset LoadedAtUtc { get; }

    public string SourceDirectory { get; }

    public IReadOnlyList<string> SourceFiles { get; }

    public ProxyConfigurationDiscoveryResponse Discovery { get; }

    public RuntimeAdminSecurityResponse AdminSecurity { get; }

    public RuntimeAcmeResponse Acme { get; }

    public RuntimeTimeoutsResponse Timeouts { get; }

    public RuntimeConnectionLimitsResponse ConnectionLimits { get; }

    public RuntimeObservabilityResponse Observability { get; }

    public RuntimeLimitsResponse Limits { get; }

    public RuntimeForwardedHeadersResponse ForwardedHeaders { get; }

    public RuntimeMetricsResponse Metrics { get; }

    public RuntimeHttp3SupportResponse Http3 { get; }

    public IReadOnlyList<RuntimeCertificateResponse> Certificates { get; }

    public IReadOnlyList<RuntimeListenerResponse> Listeners { get; }

    public IReadOnlyList<RuntimeRouteResponse> Routes { get; }

    public static ProxyConfigurationResponse FromProjection(BusinessProxyConfigurationProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new ProxyConfigurationResponse(
            version: projection.Version,
            loadedAtUtc: projection.LoadedAtUtc,
            sourceDirectory: projection.SourceDirectory,
            sourceFiles: projection.SourceFiles,
            discovery: ProxyConfigurationDiscoveryResponse.FromDiscovery(projection.Discovery),
            adminSecurity: RuntimeAdminSecurityResponse.FromProjection(projection.AdminSecurity),
            acme: RuntimeAcmeResponse.FromProjection(projection.Acme),
            timeouts: RuntimeTimeoutsResponse.FromProjection(projection.Timeouts),
            connectionLimits: RuntimeConnectionLimitsResponse.FromProjection(projection.ConnectionLimits),
            observability: RuntimeObservabilityResponse.FromProjection(projection.Observability),
            limits: RuntimeLimitsResponse.FromProjection(projection.Limits),
            forwardedHeaders: RuntimeForwardedHeadersResponse.FromProjection(projection.ForwardedHeaders),
            metrics: RuntimeMetricsResponse.FromProjection(projection.Metrics),
            http3: RuntimeHttp3SupportResponse.FromProjection(projection.Http3),
            certificates: RuntimeCertificateResponse.FromCertificates(projection.Certificates),
            listeners: RuntimeListenerResponse.FromListeners(projection.Listeners),
            routes: RuntimeRouteResponse.FromRoutes(projection.Routes));
    }
}
