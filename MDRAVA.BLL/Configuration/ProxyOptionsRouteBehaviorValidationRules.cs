using System.Text;
using MDRAVA.BLL.Http;

namespace MDRAVA.BLL.Configuration;

public static partial class ProxyOptionsValidationRules
{
    private static void ValidateRedirectPolicy(
        List<string> failures,
        string routePrefix,
        ProxyHttpsRedirectOptions redirect)
    {
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
        ProxyRedirectOptions redirect,
        IProxyUrlSyntaxPolicy urlSyntaxPolicy)
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

        if (hasTargetUrl && !urlSyntaxPolicy.IsAbsoluteUrl(redirect.TargetUrl))
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
}
