namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeMetricsOptions(
    bool Enabled,
    string EndpointPath,
    bool ProtectedByAdminAuth,
    bool IncludePerRouteLabels,
    bool IncludePerUpstreamLabels,
    bool PublicMetricsEnabled)
{
    public const string FixedAdminEndpointPath = "/admin/proxy/metrics";

    public static RuntimeMetricsOptions Default { get; } = new(
        true,
        FixedAdminEndpointPath,
        true,
        true,
        false,
        false);
}
