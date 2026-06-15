using BusinessRuntimeCacheProjection = MDRAVA.BLL.Configuration.RuntimeCacheProjection;
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
    public static RuntimeCachePolicyResponse FromProjection(BusinessRuntimeCacheProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeCachePolicyResponse(
            projection.Enabled,
            projection.MaxEntryBytes,
            projection.MaxTotalBytes,
            projection.DefaultTtl,
            projection.RespectOriginCacheControl,
            ApiResponseList.Copy(projection.VaryByHeaders),
            ApiResponseList.Copy(projection.CacheableStatusCodes),
            ApiResponseList.Copy(projection.Methods));
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
            ApiResponseList.Copy(projection.RetryOnStatusCodes),
            ApiResponseList.Copy(projection.RetryMethods),
            projection.RetryBackoff);
    }
}
