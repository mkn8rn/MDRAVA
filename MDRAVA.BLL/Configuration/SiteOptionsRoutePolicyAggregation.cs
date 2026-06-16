namespace MDRAVA.BLL.Configuration;

public static partial class SiteOptionsAggregator
{
    private static ProxyHttpsRedirectOptions MergeHttpsRedirect(
        ProxyHttpsRedirectOptions site,
        ProxyHttpsRedirectOptions route)
    {
        return new ProxyHttpsRedirectOptions
        {
            Enabled = route.Enabled ?? site.Enabled,
            StatusCode = route.StatusCode ?? site.StatusCode,
            HttpsPort = route.HttpsPort ?? site.HttpsPort
        };
    }

    private static ProxyCanonicalHostOptions MergeCanonicalHost(
        ProxyCanonicalHostOptions site,
        ProxyCanonicalHostOptions route)
    {
        return new ProxyCanonicalHostOptions
        {
            Enabled = route.Enabled ?? site.Enabled,
            TargetHost = string.IsNullOrWhiteSpace(route.TargetHost) ? site.TargetHost : route.TargetHost,
            StatusCode = route.StatusCode ?? site.StatusCode
        };
    }

    private static ProxyHeaderPolicyOptions MergeHeaderPolicy(
        ProxyHeaderPolicyOptions site,
        ProxyHeaderPolicyOptions route)
    {
        return new ProxyHeaderPolicyOptions
        {
            SetRequestHeaders = CopyHeaderFields(site.SetRequestHeaders.Concat(route.SetRequestHeaders)),
            RemoveRequestHeaders = site.RemoveRequestHeaders.Concat(route.RemoveRequestHeaders).ToList(),
            SetResponseHeaders = CopyHeaderFields(site.SetResponseHeaders.Concat(route.SetResponseHeaders)),
            RemoveResponseHeaders = site.RemoveResponseHeaders.Concat(route.RemoveResponseHeaders).ToList()
        };
    }

    private static ProxyMaintenanceOptions MergeMaintenance(
        ProxyMaintenanceOptions site,
        ProxyMaintenanceOptions route)
    {
        return new ProxyMaintenanceOptions
        {
            Enabled = route.Enabled ?? site.Enabled,
            RetryAfterSeconds = route.RetryAfterSeconds ?? site.RetryAfterSeconds,
            ContentType = string.IsNullOrWhiteSpace(route.ContentType) || string.Equals(route.ContentType, "text/plain; charset=utf-8", StringComparison.OrdinalIgnoreCase)
                ? site.ContentType
                : route.ContentType,
            Body = string.Equals(route.Body, "Service Unavailable", StringComparison.Ordinal)
                ? site.Body
                : route.Body
        };
    }

    private static ProxyCachePolicyOptions MergeCache(
        ProxyCachePolicyOptions site,
        ProxyCachePolicyOptions route)
    {
        return route.Enabled ? CopyCache(route) : CopyCache(site);
    }

    private static ProxyRetryPolicyOptions MergeRetry(
        ProxyRetryPolicyOptions site,
        ProxyRetryPolicyOptions route)
    {
        return route.Enabled ? CopyRetry(route) : CopyRetry(site);
    }

    private static ProxyRouteOverrideOptions MergeOverrides(
        ProxyRouteOverrideOptions site,
        ProxyRouteOverrideOptions route)
    {
        return new ProxyRouteOverrideOptions
        {
            MaxRequestBodyBytes = route.MaxRequestBodyBytes ?? site.MaxRequestBodyBytes,
            ClientRequestHeadTimeoutMs = route.ClientRequestHeadTimeoutMs ?? site.ClientRequestHeadTimeoutMs,
            UpstreamResponseHeadTimeoutMs = route.UpstreamResponseHeadTimeoutMs ?? site.UpstreamResponseHeadTimeoutMs,
            AccessLogEnabled = route.AccessLogEnabled ?? site.AccessLogEnabled
        };
    }
}
