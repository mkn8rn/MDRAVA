using BusinessRuntimeLogPersistenceProjection = MDRAVA.BLL.Configuration.RuntimeLogPersistenceProjection;
using BusinessRuntimeMetricsProjection = MDRAVA.BLL.Configuration.RuntimeMetricsProjection;
using BusinessRuntimeObservabilityProjection = MDRAVA.BLL.Configuration.RuntimeObservabilityProjection;

namespace MDRAVA.API.Controllers;

public sealed record RuntimeObservabilityResponse(
    bool AccessLogEnabled,
    int RecentDiagnosticsCapacity,
    RuntimeLogPersistenceResponse LogPersistence)
{
    public static RuntimeObservabilityResponse FromProjection(BusinessRuntimeObservabilityProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeObservabilityResponse(
            projection.AccessLogEnabled,
            projection.RecentDiagnosticsCapacity,
            RuntimeLogPersistenceResponse.FromProjection(projection.LogPersistence));
    }
}

public sealed record RuntimeLogPersistenceResponse(
    bool AccessLogEnabled,
    bool AdminAuditEnabled,
    long MaxFileBytes,
    int MaxFiles)
{
    public static RuntimeLogPersistenceResponse FromProjection(BusinessRuntimeLogPersistenceProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeLogPersistenceResponse(
            projection.AccessLogEnabled,
            projection.AdminAuditEnabled,
            projection.MaxFileBytes,
            projection.MaxFiles);
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
    public static RuntimeMetricsResponse FromProjection(BusinessRuntimeMetricsProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeMetricsResponse(
            projection.Enabled,
            projection.EndpointPath,
            projection.ProtectedByAdminAuth,
            projection.IncludePerRouteLabels,
            projection.IncludePerUpstreamLabels,
            projection.PublicMetricsEnabled);
    }
}
