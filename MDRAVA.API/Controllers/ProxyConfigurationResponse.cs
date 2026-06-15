using BusinessProxyConfigurationProjection =
    MDRAVA.BLL.ControlPlane.ConfigurationManagement.ProxyConfigurationProjection;

namespace MDRAVA.API.Controllers;

public sealed record ProxyConfigurationResponse(
    int Version,
    DateTimeOffset LoadedAtUtc,
    string SourceDirectory,
    IReadOnlyList<string> SourceFiles,
    ProxyConfigurationDiscoveryResponse Discovery,
    RuntimeAdminSecurityResponse AdminSecurity,
    RuntimeAcmeResponse Acme,
    RuntimeTimeoutsResponse Timeouts,
    RuntimeConnectionLimitsResponse ConnectionLimits,
    RuntimeObservabilityResponse Observability,
    RuntimeLimitsResponse Limits,
    RuntimeForwardedHeadersResponse ForwardedHeaders,
    IReadOnlyList<RuntimeCertificateResponse> Certificates,
    IReadOnlyList<RuntimeListenerResponse> Listeners,
    IReadOnlyList<RuntimeRouteResponse> Routes)
{
    public RuntimeMetricsResponse Metrics { get; init; } = null!;

    public RuntimeHttp3SupportResponse Http3 { get; init; } = null!;

    public static ProxyConfigurationResponse FromProjection(BusinessProxyConfigurationProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new ProxyConfigurationResponse(
            projection.Version,
            projection.LoadedAtUtc,
            projection.SourceDirectory,
            ApiResponseList.Copy(projection.SourceFiles),
            ProxyConfigurationDiscoveryResponse.FromDiscovery(projection.Discovery),
            RuntimeAdminSecurityResponse.FromProjection(projection.AdminSecurity),
            RuntimeAcmeResponse.FromProjection(projection.Acme),
            RuntimeTimeoutsResponse.FromProjection(projection.Timeouts),
            RuntimeConnectionLimitsResponse.FromProjection(projection.ConnectionLimits),
            RuntimeObservabilityResponse.FromProjection(projection.Observability),
            RuntimeLimitsResponse.FromProjection(projection.Limits),
            RuntimeForwardedHeadersResponse.FromProjection(projection.ForwardedHeaders),
            RuntimeCertificateResponse.FromCertificates(projection.Certificates),
            RuntimeListenerResponse.FromListeners(projection.Listeners),
            RuntimeRouteResponse.FromRoutes(projection.Routes))
        {
            Metrics = RuntimeMetricsResponse.FromProjection(projection.Metrics),
            Http3 = RuntimeHttp3SupportResponse.FromProjection(projection.Http3)
        };
    }
}
