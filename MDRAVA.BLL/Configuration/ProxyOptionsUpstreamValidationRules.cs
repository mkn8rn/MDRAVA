using MDRAVA.BLL.Http;

namespace MDRAVA.BLL.Configuration;

public static partial class ProxyOptionsValidationRules
{
    private static void ValidateUpstreamTls(
        List<string> failures,
        string upstreamPrefix,
        UpstreamTlsOptions tls,
        IProxyEndpointAddressPolicy endpointAddressPolicy)
    {
        if (string.IsNullOrWhiteSpace(tls.SniHost))
        {
            return;
        }

        if (!endpointAddressPolicy.IsValidSniHost(tls.SniHost))
        {
            failures.Add($"{upstreamPrefix}:UpstreamTls:SniHost must be a DNS host name or IP literal without scheme, path, port, whitespace, or wildcard.");
        }
    }

    private static void ValidateHealthCheck(
        List<string> failures,
        string routePrefix,
        HealthCheckOptions healthCheck)
    {
        var prefix = $"{routePrefix}:HealthCheck";

        if (string.IsNullOrWhiteSpace(healthCheck.Path) || !healthCheck.Path.StartsWith('/'))
        {
            failures.Add($"{prefix}:Path must start with '/'.");
        }

        if (healthCheck.IntervalSeconds is < 1 or > 3600)
        {
            failures.Add($"{prefix}:IntervalSeconds must be between 1 and 3600.");
        }

        if (healthCheck.TimeoutSeconds is < 1 or > 300)
        {
            failures.Add($"{prefix}:TimeoutSeconds must be between 1 and 300.");
        }

        if (healthCheck.TimeoutSeconds > healthCheck.IntervalSeconds)
        {
            failures.Add($"{prefix}:TimeoutSeconds must not exceed IntervalSeconds.");
        }

        if (healthCheck.HealthyThreshold is < 1 or > 100)
        {
            failures.Add($"{prefix}:HealthyThreshold must be between 1 and 100.");
        }

        if (healthCheck.UnhealthyThreshold is < 1 or > 100)
        {
            failures.Add($"{prefix}:UnhealthyThreshold must be between 1 and 100.");
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
}
