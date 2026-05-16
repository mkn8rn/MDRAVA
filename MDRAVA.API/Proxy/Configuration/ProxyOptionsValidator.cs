using System.Net;
using Microsoft.Extensions.Options;

namespace MDRAVA.API.Proxy.Configuration;

public sealed class ProxyOptionsValidator : IValidateOptions<ProxyOptions>
{
    public ValidateOptionsResult Validate(string? name, ProxyOptions options)
    {
        List<string> failures = [];

        if (options.Listeners.Count == 0)
        {
            failures.Add("Proxy:Listeners must contain at least one listener.");
        }

        for (var index = 0; index < options.Listeners.Count; index++)
        {
            var listener = options.Listeners[index];
            var prefix = $"Proxy:Listeners:{index}";

            if (string.IsNullOrWhiteSpace(listener.Name))
            {
                failures.Add($"{prefix}:Name is required.");
            }

            if (!IPAddress.TryParse(listener.Address, out _))
            {
                failures.Add($"{prefix}:Address must be an IP address for Phase 1.");
            }

            if (listener.Port is < 1 or > 65535)
            {
                failures.Add($"{prefix}:Port must be between 1 and 65535.");
            }

            if (listener.Backlog < 1)
            {
                failures.Add($"{prefix}:Backlog must be greater than zero.");
            }

            if (listener.MaxRequestHeadBytes is < 1024 or > 1024 * 1024)
            {
                failures.Add($"{prefix}:MaxRequestHeadBytes must be between 1024 and 1048576.");
            }

            if (listener.ForwardingBufferBytes is < 4096 or > 1024 * 1024)
            {
                failures.Add($"{prefix}:ForwardingBufferBytes must be between 4096 and 1048576.");
            }

            if (listener.MaxResponseHeadBytes is < 1024 or > 1024 * 1024)
            {
                failures.Add($"{prefix}:MaxResponseHeadBytes must be between 1024 and 1048576.");
            }

            if (listener.MaxChunkLineBytes is < 64 or > 16 * 1024)
            {
                failures.Add($"{prefix}:MaxChunkLineBytes must be between 64 and 16384.");
            }
        }

        if (options.Listeners.Count > 0 && !options.Listeners.Any(static listener => listener.Enabled))
        {
            failures.Add("Proxy:Listeners must contain at least one enabled listener.");
        }

        if (options.Routes.Count == 0)
        {
            failures.Add("Proxy:Routes must contain at least one route.");
        }

        HashSet<string> routeNames = new(StringComparer.OrdinalIgnoreCase);
        for (var routeIndex = 0; routeIndex < options.Routes.Count; routeIndex++)
        {
            var route = options.Routes[routeIndex];
            var routePrefix = $"Proxy:Routes:{routeIndex}";

            if (string.IsNullOrWhiteSpace(route.Name))
            {
                failures.Add($"{routePrefix}:Name is required.");
            }
            else if (!routeNames.Add(route.Name))
            {
                failures.Add($"{routePrefix}:Name '{route.Name}' is duplicated.");
            }

            if (string.IsNullOrWhiteSpace(route.Host))
            {
                failures.Add($"{routePrefix}:Host is required.");
            }

            if (string.IsNullOrWhiteSpace(route.PathPrefix) || !route.PathPrefix.StartsWith('/'))
            {
                failures.Add($"{routePrefix}:PathPrefix must start with '/'.");
            }

            if (!string.Equals(route.LoadBalancingPolicy, "round-robin", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"{routePrefix}:LoadBalancingPolicy must be 'round-robin' for Phase 8.");
            }

            ValidateHealthCheck(failures, routePrefix, route.HealthCheck);

            if (route.Upstreams.Count == 0)
            {
                failures.Add($"{routePrefix}:Upstreams must contain at least one upstream.");
            }

            HashSet<string> upstreamNames = new(StringComparer.OrdinalIgnoreCase);

            for (var upstreamIndex = 0; upstreamIndex < route.Upstreams.Count; upstreamIndex++)
            {
                var upstream = route.Upstreams[upstreamIndex];
                var upstreamPrefix = $"{routePrefix}:Upstreams:{upstreamIndex}";

                if (string.IsNullOrWhiteSpace(upstream.Name))
                {
                    failures.Add($"{upstreamPrefix}:Name is required.");
                }
                else if (!upstreamNames.Add(upstream.Name))
                {
                    failures.Add($"{upstreamPrefix}:Name '{upstream.Name}' is duplicated within route '{route.Name}'.");
                }

                if (string.IsNullOrWhiteSpace(upstream.Address))
                {
                    failures.Add($"{upstreamPrefix}:Address is required.");
                }

                if (upstream.Port is < 1 or > 65535)
                {
                    failures.Add($"{upstreamPrefix}:Port must be between 1 and 65535.");
                }

                if (upstream.Weight is < 1 or > 100_000)
                {
                    failures.Add($"{upstreamPrefix}:Weight must be between 1 and 100000.");
                }
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
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
}
