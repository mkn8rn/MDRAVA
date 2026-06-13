using MDRAVA.BLL.Http;

namespace MDRAVA.BLL.Configuration;

public static partial class ProxyOptionsValidationRules
{
    private const long MaximumCacheEntryBytes = 64L * 1024 * 1024;
    private const long MaximumCacheTotalBytes = 512L * 1024 * 1024;

    private static void ValidateCachePolicy(
        List<string> failures,
        string routePrefix,
        ProxyCachePolicyOptions cache,
        string routeAction)
    {
        if (cache.MaxEntryBytes < 0)
        {
            failures.Add($"{routePrefix}:Cache:MaxEntryBytes must not be negative.");
        }

        if (cache.MaxTotalBytes < 0)
        {
            failures.Add($"{routePrefix}:Cache:MaxTotalBytes must not be negative.");
        }

        if (cache.DefaultTtlSeconds < 0)
        {
            failures.Add($"{routePrefix}:Cache:DefaultTtlSeconds must not be negative.");
        }

        if (!cache.Enabled)
        {
            return;
        }

        if (!IsProxyAction(routeAction))
        {
            failures.Add($"{routePrefix}:Cache can only be enabled for proxy routes.");
        }

        if (cache.MaxEntryBytes is <= 0 or > MaximumCacheEntryBytes)
        {
            failures.Add($"{routePrefix}:Cache:MaxEntryBytes must be between 1 and {MaximumCacheEntryBytes}.");
        }

        if (cache.MaxTotalBytes is <= 0 or > MaximumCacheTotalBytes)
        {
            failures.Add($"{routePrefix}:Cache:MaxTotalBytes must be between 1 and {MaximumCacheTotalBytes}.");
        }

        if (cache.MaxEntryBytes > 0
            && cache.MaxTotalBytes > 0
            && cache.MaxEntryBytes > cache.MaxTotalBytes)
        {
            failures.Add($"{routePrefix}:Cache:MaxEntryBytes must not exceed Cache:MaxTotalBytes.");
        }

        if (cache.DefaultTtlSeconds <= 0)
        {
            failures.Add($"{routePrefix}:Cache:DefaultTtlSeconds must be greater than 0.");
        }

        for (var index = 0; index < cache.VaryByHeaders.Count; index++)
        {
            var headerName = cache.VaryByHeaders[index];
            if (string.IsNullOrWhiteSpace(headerName)
                || !ProxyHeaderPolicyOptionsValidationRules.IsValidHttpFieldName(headerName))
            {
                failures.Add($"{routePrefix}:Cache:VaryByHeaders:{index} '{headerName}' is not a valid HTTP field name.");
            }
        }

        if (cache.CacheableStatusCodes.Count == 0)
        {
            failures.Add($"{routePrefix}:Cache:CacheableStatusCodes must contain at least one status code.");
        }

        for (var index = 0; index < cache.CacheableStatusCodes.Count; index++)
        {
            var statusCode = cache.CacheableStatusCodes[index];
            if (statusCode is < 200 or > 599)
            {
                failures.Add($"{routePrefix}:Cache:CacheableStatusCodes:{index} must be an HTTP response status code.");
            }
        }

        if (cache.Methods.Count == 0)
        {
            failures.Add($"{routePrefix}:Cache:Methods must contain GET, HEAD, or both.");
        }

        for (var index = 0; index < cache.Methods.Count; index++)
        {
            var method = cache.Methods[index];
            if (!ProxyRequestMethodPolicy.IsSafeReadMethod(method))
            {
                failures.Add($"{routePrefix}:Cache:Methods:{index} must be GET or HEAD.");
            }
        }
    }
}
