using BusinessRuntimeCachePolicy = MDRAVA.BLL.Configuration.RuntimeCachePolicy;
using BusinessRuntimeRetryPolicy = MDRAVA.BLL.Configuration.RuntimeRetryPolicy;

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
    public static RuntimeRetryPolicyResponse FromPolicy(BusinessRuntimeRetryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return new RuntimeRetryPolicyResponse(
            policy.Enabled,
            policy.MaxAttempts,
            policy.PerAttemptTimeout,
            policy.RetryOnConnectFailure,
            policy.RetryOnUpstreamResponseHeadTimeout,
            policy.RetryOnStatusCodes.ToArray(),
            policy.RetryMethods.ToArray(),
            policy.RetryBackoff);
    }
}
