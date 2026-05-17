using System.Net;
using System.Text;
using Microsoft.Extensions.Options;

namespace MDRAVA.API.Proxy.Configuration;

public sealed class ProxyOptionsValidator : IValidateOptions<ProxyOptions>
{
    private const int MaxGeneratedBodyBytes = 64 * 1024;
    private const long MaximumCacheEntryBytes = 64L * 1024 * 1024;
    private const long MaximumCacheTotalBytes = 512L * 1024 * 1024;
    private static readonly HashSet<int> RedirectStatusCodes = [301, 302, 303, 307, 308];
    private static readonly HashSet<string> RestrictedHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection",
        "Keep-Alive",
        "Proxy-Authenticate",
        "Proxy-Authorization",
        "TE",
        "Trailer",
        "Transfer-Encoding",
        "Upgrade",
        "Content-Length",
        "Host",
        "X-Request-Id"
    };

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

            var routeAction = string.IsNullOrWhiteSpace(route.Action) ? "proxy" : route.Action;
            if (!IsRouteAction(routeAction))
            {
                failures.Add($"{routePrefix}:Action must be 'proxy', 'redirect', or 'staticResponse'.");
            }

            if (IsProxyAction(routeAction)
                && !string.Equals(route.LoadBalancingPolicy, "round-robin", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"{routePrefix}:LoadBalancingPolicy must be 'round-robin' for Phase 8.");
            }

            if (IsProxyAction(routeAction))
            {
                ValidateHealthCheck(failures, routePrefix, route.HealthCheck);
            }

            ValidateRedirectPolicy(failures, routePrefix, route.HttpsRedirect, allowEmptyTarget: true);
            ValidateCanonicalHost(failures, routePrefix, route.CanonicalHost);
            ValidateHeaderPolicy(failures, routePrefix, route.HeaderPolicy);
            ValidatePathRewrite(failures, routePrefix, route.PathRewrite);
            ValidateMaintenance(failures, routePrefix, route.Maintenance);
            ValidateCachePolicy(failures, routePrefix, route.Cache, routeAction);
            ValidateOverrides(failures, routePrefix, route.Overrides);

            if (IsProxyAction(routeAction) && route.Upstreams.Count == 0)
            {
                failures.Add($"{routePrefix}:Upstreams must contain at least one upstream.");
            }

            if (IsRedirectAction(routeAction))
            {
                ValidateRedirectRoute(failures, routePrefix, route.Redirect);
            }

            if (IsStaticResponseAction(routeAction))
            {
                ValidateStaticResponse(failures, routePrefix, route.StaticResponse);
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
                else if (IsAmbiguousUpstreamAddress(upstream.Address))
                {
                    failures.Add($"{upstreamPrefix}:Address must be a host name or IP literal without scheme, path, whitespace, or embedded port.");
                }

                var upstreamScheme = string.IsNullOrWhiteSpace(upstream.Scheme) ? "http" : upstream.Scheme;
                if (!IsSupportedUpstreamScheme(upstreamScheme))
                {
                    failures.Add($"{upstreamPrefix}:Scheme must be 'http' or 'https'.");
                }

                if (upstream.Port is < 1 or > 65535)
                {
                    failures.Add($"{upstreamPrefix}:Port must be between 1 and 65535.");
                }

                if (upstream.Weight is < 1 or > 100_000)
                {
                    failures.Add($"{upstreamPrefix}:Weight must be between 1 and 100000.");
                }

                ValidateUpstreamTls(failures, upstreamPrefix, upstream.UpstreamTls);
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsRouteAction(string action)
    {
        return IsProxyAction(action) || IsRedirectAction(action) || IsStaticResponseAction(action);
    }

    private static bool IsProxyAction(string action)
    {
        return string.Equals(action, "proxy", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRedirectAction(string action)
    {
        return string.Equals(action, "redirect", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStaticResponseAction(string action)
    {
        return string.Equals(action, "staticResponse", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSupportedUpstreamScheme(string scheme)
    {
        return string.Equals(scheme, "http", StringComparison.OrdinalIgnoreCase)
            || string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAmbiguousUpstreamAddress(string value)
    {
        var address = value.Trim();
        if (address.Length == 0
            || address.Contains("://", StringComparison.Ordinal)
            || address.Contains('/', StringComparison.Ordinal)
            || address.Contains('\\', StringComparison.Ordinal)
            || address.Any(static character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            return true;
        }

        if (IPAddress.TryParse(address, out _))
        {
            return false;
        }

        return address.Contains(':', StringComparison.Ordinal);
    }

    private static void ValidateUpstreamTls(
        List<string> failures,
        string upstreamPrefix,
        UpstreamTlsOptions tls)
    {
        if (string.IsNullOrWhiteSpace(tls.SniHost))
        {
            return;
        }

        if (!IsValidSniHost(tls.SniHost))
        {
            failures.Add($"{upstreamPrefix}:UpstreamTls:SniHost must be a DNS host name or IP literal without scheme, path, port, whitespace, or wildcard.");
        }
    }

    private static bool IsValidSniHost(string value)
    {
        var host = value.Trim();
        if (host.Length is 0 or > 253
            || host.StartsWith("*.", StringComparison.Ordinal)
            || host.Contains('/', StringComparison.Ordinal)
            || host.Contains('\\', StringComparison.Ordinal)
            || host.Any(static character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            return false;
        }

        if (IPAddress.TryParse(host, out _))
        {
            return true;
        }

        return !host.Contains(':', StringComparison.Ordinal)
            && Uri.CheckHostName(host) is UriHostNameType.Dns or UriHostNameType.IPv4;
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

    private static void ValidateRedirectPolicy(
        List<string> failures,
        string routePrefix,
        ProxyHttpsRedirectOptions redirect,
        bool allowEmptyTarget)
    {
        _ = allowEmptyTarget;
        if (redirect.StatusCode.HasValue && !RedirectStatusCodes.Contains(redirect.StatusCode.Value))
        {
            failures.Add($"{routePrefix}:HttpsRedirect:StatusCode must be one of 301, 302, 303, 307, or 308.");
        }

        if (redirect.HttpsPort is < 1 or > 65535)
        {
            failures.Add($"{routePrefix}:HttpsRedirect:HttpsPort must be between 1 and 65535.");
        }
    }

    private static void ValidateCanonicalHost(
        List<string> failures,
        string routePrefix,
        ProxyCanonicalHostOptions canonicalHost)
    {
        if (canonicalHost.StatusCode.HasValue && !RedirectStatusCodes.Contains(canonicalHost.StatusCode.Value))
        {
            failures.Add($"{routePrefix}:CanonicalHost:StatusCode must be one of 301, 302, 303, 307, or 308.");
        }

        var enabled = canonicalHost.Enabled == true || !string.IsNullOrWhiteSpace(canonicalHost.TargetHost);
        if (!enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(canonicalHost.TargetHost))
        {
            failures.Add($"{routePrefix}:CanonicalHost:TargetHost is required when canonical host redirect is enabled.");
            return;
        }

        if (canonicalHost.TargetHost.Contains('/', StringComparison.Ordinal)
            || canonicalHost.TargetHost.Contains('\\', StringComparison.Ordinal)
            || canonicalHost.TargetHost.Any(char.IsWhiteSpace))
        {
            failures.Add($"{routePrefix}:CanonicalHost:TargetHost must be a host name, not a URL or path.");
        }
    }

    private static void ValidateHeaderPolicy(
        List<string> failures,
        string routePrefix,
        ProxyHeaderPolicyOptions policy)
    {
        ValidateHeaderSetRules(failures, $"{routePrefix}:HeaderPolicy:SetRequestHeaders", policy.SetRequestHeaders);
        ValidateHeaderSetRules(failures, $"{routePrefix}:HeaderPolicy:SetResponseHeaders", policy.SetResponseHeaders);
        ValidateHeaderRemoveRules(failures, $"{routePrefix}:HeaderPolicy:RemoveRequestHeaders", policy.RemoveRequestHeaders);
        ValidateHeaderRemoveRules(failures, $"{routePrefix}:HeaderPolicy:RemoveResponseHeaders", policy.RemoveResponseHeaders);
    }

    private static void ValidateHeaderSetRules(
        List<string> failures,
        string prefix,
        IReadOnlyList<ProxyHeaderSetOptions> rules)
    {
        for (var index = 0; index < rules.Count; index++)
        {
            var rule = rules[index];
            var rulePrefix = $"{prefix}:{index}";
            ValidateHeaderName(failures, $"{rulePrefix}:Name", rule.Name);
            if (rule.Value.Any(static character => character is '\r' or '\n'))
            {
                failures.Add($"{rulePrefix}:Value must not contain CR or LF.");
            }
        }
    }

    private static void ValidateHeaderRemoveRules(
        List<string> failures,
        string prefix,
        IReadOnlyList<string> headerNames)
    {
        for (var index = 0; index < headerNames.Count; index++)
        {
            ValidateHeaderName(failures, $"{prefix}:{index}", headerNames[index]);
        }
    }

    private static void ValidateHeaderName(List<string> failures, string prefix, string headerName)
    {
        if (string.IsNullOrWhiteSpace(headerName))
        {
            failures.Add($"{prefix} is required.");
            return;
        }

        if (RestrictedHeaderNames.Contains(headerName))
        {
            failures.Add($"{prefix} '{headerName}' is restricted and cannot be modified by header policy.");
            return;
        }

        if (!IsValidHttpFieldName(headerName))
        {
            failures.Add($"{prefix} '{headerName}' is not a valid HTTP field name.");
            return;
        }
    }

    private static bool IsValidHttpFieldName(string headerName)
    {
        foreach (var character in headerName)
        {
            var valid = character is >= '!' and <= '~'
                && character is not '(' and not ')' and not '<' and not '>' and not '@'
                && character is not ',' and not ';' and not ':' and not '\\' and not '"'
                && character is not '/' and not '[' and not ']' and not '?' and not '='
                && character is not '{' and not '}';
            if (!valid)
            {
                return false;
            }
        }

        return true;
    }

    private static void ValidatePathRewrite(
        List<string> failures,
        string routePrefix,
        ProxyPathRewriteOptions rewrite)
    {
        if (!string.IsNullOrWhiteSpace(rewrite.StripPrefix) && !rewrite.StripPrefix.StartsWith('/'))
        {
            failures.Add($"{routePrefix}:PathRewrite:StripPrefix must start with '/'.");
        }

        if (!string.IsNullOrWhiteSpace(rewrite.ReplacePrefix) && !rewrite.ReplacePrefix.StartsWith('/'))
        {
            failures.Add($"{routePrefix}:PathRewrite:ReplacePrefix must start with '/'.");
        }

        if (!string.IsNullOrWhiteSpace(rewrite.Replacement) && !rewrite.Replacement.StartsWith('/'))
        {
            failures.Add($"{routePrefix}:PathRewrite:Replacement must start with '/'.");
        }

        if (!string.IsNullOrWhiteSpace(rewrite.StripPrefix)
            && !string.IsNullOrWhiteSpace(rewrite.ReplacePrefix))
        {
            failures.Add($"{routePrefix}:PathRewrite must not configure both StripPrefix and ReplacePrefix.");
        }

        if (!string.IsNullOrWhiteSpace(rewrite.ReplacePrefix)
            && string.IsNullOrWhiteSpace(rewrite.Replacement))
        {
            failures.Add($"{routePrefix}:PathRewrite:Replacement is required when ReplacePrefix is configured.");
        }
    }

    private static void ValidateRedirectRoute(
        List<string> failures,
        string routePrefix,
        ProxyRedirectOptions redirect)
    {
        var statusCode = redirect.StatusCode ?? 308;
        if (!RedirectStatusCodes.Contains(statusCode))
        {
            failures.Add($"{routePrefix}:Redirect:StatusCode must be one of 301, 302, 303, 307, or 308.");
        }

        var hasTargetUrl = !string.IsNullOrWhiteSpace(redirect.TargetUrl);
        var hasTargetPath = !string.IsNullOrWhiteSpace(redirect.TargetPath);
        if (hasTargetUrl == hasTargetPath)
        {
            failures.Add($"{routePrefix}:Redirect must set exactly one of TargetUrl or TargetPath.");
            return;
        }

        if (hasTargetUrl && !Uri.TryCreate(redirect.TargetUrl, UriKind.Absolute, out _))
        {
            failures.Add($"{routePrefix}:Redirect:TargetUrl must be an absolute URL.");
        }

        if (hasTargetPath && !redirect.TargetPath.StartsWith('/'))
        {
            failures.Add($"{routePrefix}:Redirect:TargetPath must start with '/'.");
        }
    }

    private static void ValidateStaticResponse(
        List<string> failures,
        string routePrefix,
        ProxyStaticResponseOptions response)
    {
        if (response.StatusCode is < 200 or > 599)
        {
            failures.Add($"{routePrefix}:StaticResponse:StatusCode must be between 200 and 599.");
        }

        if (string.IsNullOrWhiteSpace(response.ContentType)
            || response.ContentType.Any(static character => character is '\r' or '\n'))
        {
            failures.Add($"{routePrefix}:StaticResponse:ContentType must be a non-empty single-line value.");
        }

        if (Encoding.UTF8.GetByteCount(response.Body) > MaxGeneratedBodyBytes)
        {
            failures.Add($"{routePrefix}:StaticResponse:Body must not exceed {MaxGeneratedBodyBytes} UTF-8 bytes.");
        }
    }

    private static void ValidateMaintenance(
        List<string> failures,
        string routePrefix,
        ProxyMaintenanceOptions maintenance)
    {
        if (maintenance.RetryAfterSeconds is < 0 or > 86400)
        {
            failures.Add($"{routePrefix}:Maintenance:RetryAfterSeconds must be between 0 and 86400.");
        }

        if (string.IsNullOrWhiteSpace(maintenance.ContentType)
            || maintenance.ContentType.Any(static character => character is '\r' or '\n'))
        {
            failures.Add($"{routePrefix}:Maintenance:ContentType must be a non-empty single-line value.");
        }

        if (Encoding.UTF8.GetByteCount(maintenance.Body) > MaxGeneratedBodyBytes)
        {
            failures.Add($"{routePrefix}:Maintenance:Body must not exceed {MaxGeneratedBodyBytes} UTF-8 bytes.");
        }
    }

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
            if (string.IsNullOrWhiteSpace(headerName) || !IsValidHttpFieldName(headerName))
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
            if (!string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"{routePrefix}:Cache:Methods:{index} must be GET or HEAD.");
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
