using MDRAVA.BLL.Http;

namespace MDRAVA.BLL.Configuration;

public static partial class ProxyOptionsValidationRules
{
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

    private static void ValidateRetryPolicy(
        List<string> failures,
        string routePrefix,
        ProxyRetryPolicyOptions retry,
        string routeAction)
    {
        if (retry.MaxAttempts is < 1 or > 5)
        {
            failures.Add($"{routePrefix}:Retry:MaxAttempts must be between 1 and 5.");
        }

        if (retry.PerAttemptTimeoutMs is < 0 or > 10 * 60 * 1000)
        {
            failures.Add($"{routePrefix}:Retry:PerAttemptTimeoutMs must be between 0 and 600000 milliseconds when configured.");
        }

        if (retry.RetryBackoffMilliseconds is < 0 or > 60_000)
        {
            failures.Add($"{routePrefix}:Retry:RetryBackoffMilliseconds must be between 0 and 60000.");
        }

        if (!retry.Enabled)
        {
            return;
        }

        if (!IsProxyAction(routeAction))
        {
            failures.Add($"{routePrefix}:Retry can only be enabled for proxy routes.");
        }

        if (retry.MaxAttempts < 2)
        {
            failures.Add($"{routePrefix}:Retry:MaxAttempts must be at least 2 when retry is enabled.");
        }

        if (retry.RetryMethods.Count == 0)
        {
            failures.Add($"{routePrefix}:Retry:RetryMethods must contain GET, HEAD, or both.");
        }

        for (var index = 0; index < retry.RetryMethods.Count; index++)
        {
            var method = retry.RetryMethods[index];
            if (!ProxyRequestMethodPolicy.IsSafeReadMethod(method))
            {
                failures.Add($"{routePrefix}:Retry:RetryMethods:{index} must be GET or HEAD.");
            }
        }

        for (var index = 0; index < retry.RetryOnStatusCodes.Count; index++)
        {
            var statusCode = retry.RetryOnStatusCodes[index];
            if (statusCode is < 500 or > 599)
            {
                failures.Add($"{routePrefix}:Retry:RetryOnStatusCodes:{index} must be a 5xx HTTP response status code.");
            }
        }
    }

    private static void ValidateCircuitBreaker(
        List<string> failures,
        string upstreamPrefix,
        ProxyCircuitBreakerOptions circuitBreaker)
    {
        if (circuitBreaker.FailureThreshold is < 1 or > 1000)
        {
            failures.Add($"{upstreamPrefix}:CircuitBreaker:FailureThreshold must be between 1 and 1000.");
        }

        if (circuitBreaker.SamplingWindowSeconds is < 1 or > 3600)
        {
            failures.Add($"{upstreamPrefix}:CircuitBreaker:SamplingWindowSeconds must be between 1 and 3600.");
        }

        if (circuitBreaker.OpenDurationSeconds is < 1 or > 3600)
        {
            failures.Add($"{upstreamPrefix}:CircuitBreaker:OpenDurationSeconds must be between 1 and 3600.");
        }

        if (circuitBreaker.HalfOpenMaxAttempts is < 1 or > 100)
        {
            failures.Add($"{upstreamPrefix}:CircuitBreaker:HalfOpenMaxAttempts must be between 1 and 100.");
        }

        for (var index = 0; index < circuitBreaker.FailureStatusCodes.Count; index++)
        {
            var statusCode = circuitBreaker.FailureStatusCodes[index];
            if (statusCode is < 500 or > 599)
            {
                failures.Add($"{upstreamPrefix}:CircuitBreaker:FailureStatusCodes:{index} must be a 5xx HTTP response status code.");
            }
        }
    }

    private static void ValidateOverrides(
        List<string> failures,
        string routePrefix,
        ProxyRouteOverrideOptions overrides)
    {
        if (overrides.MaxRequestBodyBytes is < 0 or > 1L * 1024 * 1024 * 1024 * 1024)
        {
            failures.Add($"{routePrefix}:Overrides:MaxRequestBodyBytes must be between 0 and 1099511627776.");
        }

        if (overrides.ClientRequestHeadTimeoutMs.HasValue)
        {
            ValidateOverrideTimeout(failures, $"{routePrefix}:Overrides:ClientRequestHeadTimeoutMs", overrides.ClientRequestHeadTimeoutMs.Value);
        }

        if (overrides.UpstreamResponseHeadTimeoutMs.HasValue)
        {
            ValidateOverrideTimeout(failures, $"{routePrefix}:Overrides:UpstreamResponseHeadTimeoutMs", overrides.UpstreamResponseHeadTimeoutMs.Value);
        }
    }

    private static void ValidateOverrideTimeout(List<string> failures, string name, int value)
    {
        if (value is < 100 or > 10 * 60 * 1000)
        {
            failures.Add($"{name} must be between 100 and 600000 milliseconds.");
        }
    }
}
