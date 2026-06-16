using MDRAVA.BLL.Http;

namespace MDRAVA.BLL.Configuration;

public static partial class ProxyConfigurationRuntimeMapper
{
    private static RuntimeRoute ToRuntimeRoute(ProxyRouteOptions route, ProxyOperationalOptions operationalOptions)
    {
        var action = ParseAction(route.Action);
        return new RuntimeRoute(
            route.Name,
            route.Host,
            route.PathPrefix,
            action,
            string.IsNullOrWhiteSpace(route.LoadBalancingPolicy) ? "round-robin" : route.LoadBalancingPolicy,
            new RuntimeHealthCheckOptions(
                route.HealthCheck.Enabled,
                string.IsNullOrWhiteSpace(route.HealthCheck.Path) ? "/health" : route.HealthCheck.Path,
                TimeSpan.FromSeconds(route.HealthCheck.IntervalSeconds),
                TimeSpan.FromSeconds(route.HealthCheck.TimeoutSeconds),
                route.HealthCheck.HealthyThreshold,
                route.HealthCheck.UnhealthyThreshold),
            route.Upstreams
                .Select(upstream => new RuntimeUpstream(
                    route.Name,
                    upstream.Name,
                    string.IsNullOrWhiteSpace(upstream.Scheme) ? "http" : upstream.Scheme.ToLowerInvariant(),
                    string.IsNullOrWhiteSpace(upstream.Protocol) ? RuntimeUpstreamProtocol.Http1 : upstream.Protocol.Trim().ToLowerInvariant(),
                    upstream.Address,
                    upstream.Port,
                    upstream.Weight,
                    new RuntimeUpstreamTlsOptions(
                        upstream.UpstreamTls.ValidateCertificate,
                        string.IsNullOrWhiteSpace(upstream.UpstreamTls.SniHost) ? null : upstream.UpstreamTls.SniHost.Trim()),
                    ToRuntimeCircuitBreaker(upstream.CircuitBreaker)))
                .ToArray(),
            new RuntimeHttpsRedirectPolicy(
                route.HttpsRedirect.Enabled ?? false,
                route.HttpsRedirect.StatusCode ?? 308,
                route.HttpsRedirect.HttpsPort),
            new RuntimeCanonicalHostPolicy(
                route.CanonicalHost.Enabled ?? !string.IsNullOrWhiteSpace(route.CanonicalHost.TargetHost),
                route.CanonicalHost.TargetHost,
                route.CanonicalHost.StatusCode ?? 308),
            new RuntimeHeaderPolicy(
                route.HeaderPolicy.SetRequestHeaders
                    .Select(static header => new ProxyHeaderField(header.Name, header.Value))
                    .ToArray(),
                route.HeaderPolicy.RemoveRequestHeaders.ToArray(),
                route.HeaderPolicy.SetResponseHeaders
                    .Select(static header => new ProxyHeaderField(header.Name, header.Value))
                    .ToArray(),
                route.HeaderPolicy.RemoveResponseHeaders.ToArray()),
            new RuntimePathRewritePolicy(
                route.PathRewrite.StripPrefix,
                route.PathRewrite.ReplacePrefix,
                route.PathRewrite.Replacement),
            new RuntimeRedirectPolicy(
                route.Redirect.StatusCode ?? 308,
                route.Redirect.TargetUrl,
                route.Redirect.TargetPath,
                route.Redirect.PreserveQuery),
            new RuntimeStaticResponse(
                route.StaticResponse.StatusCode,
                route.StaticResponse.ContentType,
                route.StaticResponse.Body),
            new RuntimeMaintenancePolicy(
                route.Maintenance.Enabled ?? false,
                route.Maintenance.RetryAfterSeconds,
                route.Maintenance.ContentType,
                route.Maintenance.Body),
            new RuntimeCachePolicy(
                route.Cache.Enabled,
                route.Cache.MaxEntryBytes,
                route.Cache.MaxTotalBytes,
                TimeSpan.FromSeconds(route.Cache.DefaultTtlSeconds),
                route.Cache.RespectOriginCacheControl,
                route.Cache.VaryByHeaders
                    .Where(static header => !string.IsNullOrWhiteSpace(header))
                    .Select(static header => header.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                route.Cache.CacheableStatusCodes
                    .Distinct()
                    .Order()
                    .ToArray(),
                route.Cache.Methods
                    .Where(static method => !string.IsNullOrWhiteSpace(method))
                    .Select(static method => method.Trim().ToUpperInvariant())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()),
            new RuntimeRouteResolvedOptions(
                route.Overrides.MaxRequestBodyBytes ?? operationalOptions.Limits.MaxRequestBodyBytes,
                TimeSpan.FromMilliseconds(route.Overrides.ClientRequestHeadTimeoutMs ?? operationalOptions.Timeouts.ClientRequestHeadTimeoutMs),
                TimeSpan.FromMilliseconds(route.Overrides.UpstreamResponseHeadTimeoutMs ?? operationalOptions.Timeouts.UpstreamResponseHeadTimeoutMs),
                route.Overrides.AccessLogEnabled ?? operationalOptions.Observability.AccessLogEnabled),
            route.SiteName,
            ToRuntimeRetry(route.Retry));
    }

    private static RuntimeRetryPolicy ToRuntimeRetry(ProxyRetryPolicyOptions retry)
    {
        return new RuntimeRetryPolicy(
            retry.Enabled,
            Math.Max(1, retry.MaxAttempts),
            retry.PerAttemptTimeoutMs.HasValue && retry.PerAttemptTimeoutMs.Value > 0
                ? TimeSpan.FromMilliseconds(retry.PerAttemptTimeoutMs.Value)
                : null,
            retry.RetryOnConnectFailure,
            retry.RetryOnUpstreamResponseHeadTimeout,
            retry.RetryOnStatusCodes
                .Distinct()
                .Order()
                .ToArray(),
            retry.RetryMethods
                .Where(static method => !string.IsNullOrWhiteSpace(method))
                .Select(static method => method.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            TimeSpan.FromMilliseconds(Math.Max(0, retry.RetryBackoffMilliseconds)));
    }

    private static RuntimeCircuitBreakerPolicy ToRuntimeCircuitBreaker(ProxyCircuitBreakerOptions circuitBreaker)
    {
        return new RuntimeCircuitBreakerPolicy(
            circuitBreaker.Enabled,
            circuitBreaker.FailureThreshold,
            TimeSpan.FromSeconds(circuitBreaker.SamplingWindowSeconds),
            TimeSpan.FromSeconds(circuitBreaker.OpenDurationSeconds),
            circuitBreaker.HalfOpenMaxAttempts,
            circuitBreaker.FailureStatusCodes
                .Distinct()
                .Order()
                .ToArray());
    }

    private static RuntimeRouteAction ParseAction(string action)
    {
        if (string.Equals(action, "redirect", StringComparison.OrdinalIgnoreCase))
        {
            return RuntimeRouteAction.Redirect;
        }

        if (string.Equals(action, "staticResponse", StringComparison.OrdinalIgnoreCase))
        {
            return RuntimeRouteAction.StaticResponse;
        }

        return RuntimeRouteAction.Proxy;
    }
}
