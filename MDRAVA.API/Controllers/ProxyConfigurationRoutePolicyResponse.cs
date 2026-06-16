using BusinessRuntimeCacheProjection = MDRAVA.BLL.Configuration.RuntimeCacheProjection;
using BusinessRuntimeRetryProjection = MDRAVA.BLL.Configuration.RuntimeRetryProjection;

namespace MDRAVA.API.Controllers;

public sealed record RuntimeCachePolicyResponse
{
    public RuntimeCachePolicyResponse(
        bool enabled,
        long maxEntryBytes,
        long maxTotalBytes,
        TimeSpan defaultTtl,
        bool respectOriginCacheControl,
        IReadOnlyList<string> varyByHeaders,
        IReadOnlyList<int> cacheableStatusCodes,
        IReadOnlyList<string> methods)
    {
        Enabled = enabled;
        MaxEntryBytes = maxEntryBytes;
        MaxTotalBytes = maxTotalBytes;
        DefaultTtl = defaultTtl;
        RespectOriginCacheControl = respectOriginCacheControl;
        VaryByHeaders = ApiResponseList.Copy(varyByHeaders);
        CacheableStatusCodes = ApiResponseList.Copy(cacheableStatusCodes);
        Methods = ApiResponseList.Copy(methods);
    }

    public bool Enabled { get; }

    public long MaxEntryBytes { get; }

    public long MaxTotalBytes { get; }

    public TimeSpan DefaultTtl { get; }

    public bool RespectOriginCacheControl { get; }

    public IReadOnlyList<string> VaryByHeaders { get; }

    public IReadOnlyList<int> CacheableStatusCodes { get; }

    public IReadOnlyList<string> Methods { get; }

    public static RuntimeCachePolicyResponse FromProjection(BusinessRuntimeCacheProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeCachePolicyResponse(
            enabled: projection.Enabled,
            maxEntryBytes: projection.MaxEntryBytes,
            maxTotalBytes: projection.MaxTotalBytes,
            defaultTtl: projection.DefaultTtl,
            respectOriginCacheControl: projection.RespectOriginCacheControl,
            varyByHeaders: projection.VaryByHeaders,
            cacheableStatusCodes: projection.CacheableStatusCodes,
            methods: projection.Methods);
    }
}

public sealed record RuntimeRetryPolicyResponse
{
    public RuntimeRetryPolicyResponse(
        bool enabled,
        int maxAttempts,
        TimeSpan? perAttemptTimeout,
        bool retryOnConnectFailure,
        bool retryOnUpstreamResponseHeadTimeout,
        IReadOnlyList<int> retryOnStatusCodes,
        IReadOnlyList<string> retryMethods,
        TimeSpan retryBackoff)
    {
        Enabled = enabled;
        MaxAttempts = maxAttempts;
        PerAttemptTimeout = perAttemptTimeout;
        RetryOnConnectFailure = retryOnConnectFailure;
        RetryOnUpstreamResponseHeadTimeout = retryOnUpstreamResponseHeadTimeout;
        RetryOnStatusCodes = ApiResponseList.Copy(retryOnStatusCodes);
        RetryMethods = ApiResponseList.Copy(retryMethods);
        RetryBackoff = retryBackoff;
    }

    public bool Enabled { get; }

    public int MaxAttempts { get; }

    public TimeSpan? PerAttemptTimeout { get; }

    public bool RetryOnConnectFailure { get; }

    public bool RetryOnUpstreamResponseHeadTimeout { get; }

    public IReadOnlyList<int> RetryOnStatusCodes { get; }

    public IReadOnlyList<string> RetryMethods { get; }

    public TimeSpan RetryBackoff { get; }

    public static RuntimeRetryPolicyResponse FromProjection(BusinessRuntimeRetryProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeRetryPolicyResponse(
            enabled: projection.Enabled,
            maxAttempts: projection.MaxAttempts,
            perAttemptTimeout: projection.PerAttemptTimeout,
            retryOnConnectFailure: projection.RetryOnConnectFailure,
            retryOnUpstreamResponseHeadTimeout: projection.RetryOnUpstreamResponseHeadTimeout,
            retryOnStatusCodes: projection.RetryOnStatusCodes,
            retryMethods: projection.RetryMethods,
            retryBackoff: projection.RetryBackoff);
    }
}
