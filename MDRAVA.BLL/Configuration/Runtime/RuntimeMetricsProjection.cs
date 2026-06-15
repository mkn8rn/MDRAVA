namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeMetricsProjection(
    bool Enabled,
    string EndpointPath,
    bool ProtectedByAdminAuth,
    bool IncludePerRouteLabels,
    bool IncludePerUpstreamLabels,
    bool PublicMetricsEnabled)
{
    public const string FixedAdminEndpointPath = "/admin/proxy/metrics";

    public static RuntimeMetricsProjection Default { get; } = new(
        true,
        FixedAdminEndpointPath,
        true,
        true,
        false,
        false);
}
