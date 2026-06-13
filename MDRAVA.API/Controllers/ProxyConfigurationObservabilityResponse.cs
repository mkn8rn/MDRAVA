using BusinessRuntimeLogPersistenceOptions = MDRAVA.BLL.Configuration.RuntimeLogPersistenceOptions;
using BusinessRuntimeMetricsOptions = MDRAVA.BLL.Configuration.RuntimeMetricsOptions;
using BusinessRuntimeObservabilityOptions = MDRAVA.BLL.Configuration.RuntimeObservabilityOptions;

namespace MDRAVA.API.Controllers;

public sealed record RuntimeObservabilityResponse(
    bool AccessLogEnabled,
    int RecentDiagnosticsCapacity,
    RuntimeLogPersistenceResponse LogPersistence)
{
    public static RuntimeObservabilityResponse FromOptions(BusinessRuntimeObservabilityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new RuntimeObservabilityResponse(
            options.AccessLogEnabled,
            options.RecentDiagnosticsCapacity,
            RuntimeLogPersistenceResponse.FromOptions(options.LogPersistence));
    }
}

public sealed record RuntimeLogPersistenceResponse(
    bool AccessLogEnabled,
    bool AdminAuditEnabled,
    long MaxFileBytes,
    int MaxFiles)
{
    public static RuntimeLogPersistenceResponse FromOptions(BusinessRuntimeLogPersistenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new RuntimeLogPersistenceResponse(
            options.AccessLogEnabled,
            options.AdminAuditEnabled,
            options.MaxFileBytes,
            options.MaxFiles);
    }
}

public sealed record RuntimeMetricsResponse(
    bool Enabled,
    string EndpointPath,
    bool ProtectedByAdminAuth,
    bool IncludePerRouteLabels,
    bool IncludePerUpstreamLabels,
    bool PublicMetricsEnabled)
{
    public static RuntimeMetricsResponse FromOptions(BusinessRuntimeMetricsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new RuntimeMetricsResponse(
            options.Enabled,
            options.EndpointPath,
            options.ProtectedByAdminAuth,
            options.IncludePerRouteLabels,
            options.IncludePerUpstreamLabels,
            options.PublicMetricsEnabled);
    }
}
