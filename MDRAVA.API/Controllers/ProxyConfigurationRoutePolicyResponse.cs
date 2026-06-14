using BusinessRuntimeCachePolicy = MDRAVA.BLL.Configuration.RuntimeCachePolicy;
using BusinessRuntimeRetryProjection = MDRAVA.BLL.Configuration.RuntimeRetryProjection;

namespace MDRAVA.API.Controllers;

public sealed record RuntimeCachePolicyResponse(
    bool Enabled,
    long MaxEntryBytes,
    long MaxTotalBytes,
    TimeSpan DefaultTtl,
    bool RespectOriginCacheControl,
    IReadOnlyList<string> VaryByHeaders,
    IReadOnlyList<int> CacheableStatusCodes,
    IReadOnlyList<string> Methods)
{
    public static RuntimeCachePolicyResponse FromPolicy(BusinessRuntimeCachePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return new RuntimeCachePolicyResponse(
            policy.Enabled,
            policy.MaxEntryBytes,
            policy.MaxTotalBytes,
            policy.DefaultTtl,
            policy.RespectOriginCacheControl,
            policy.VaryByHeaders.ToArray(),
            policy.CacheableStatusCodes.ToArray(),
            policy.Methods.ToArray());
    }
}

public sealed record RuntimeRetryPolicyResponse(
    bool Enabled,
    int MaxAttempts,
    TimeSpan? PerAttemptTimeout,
    bool RetryOnConnectFailure,
    bool RetryOnUpstreamResponseHeadTimeout,
    IReadOnlyList<int> RetryOnStatusCodes,
    IReadOnlyList<string> RetryMethods,
    TimeSpan RetryBackoff)
{
    public static RuntimeRetryPolicyResponse FromProjection(BusinessRuntimeRetryProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeRetryPolicyResponse(
            projection.Enabled,
            projection.MaxAttempts,
            projection.PerAttemptTimeout,
            projection.RetryOnConnectFailure,
            projection.RetryOnUpstreamResponseHeadTimeout,
            projection.RetryOnStatusCodes.ToArray(),
            projection.RetryMethods.ToArray(),
            projection.RetryBackoff);
    }
}
