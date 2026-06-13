namespace MDRAVA.BLL.Configuration;

public static partial class ProxyOperationalOptionsValidationRules
{
    private static void ValidateForwardedHeaders(
        List<string> failures,
        ProxyForwardedHeadersOptions options,
        IProxyTrustedProxyPolicy trustedProxyPolicy)
    {
        HashSet<string> entries = new(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < options.TrustedProxies.Count; index++)
        {
            var entry = options.TrustedProxies[index];
            var prefix = $"Proxy:ForwardedHeaders:TrustedProxies:{index}";
            if (string.IsNullOrWhiteSpace(entry))
            {
                failures.Add($"{prefix} must not be empty.");
                continue;
            }

            if (!entries.Add(entry.Trim()))
            {
                failures.Add($"{prefix} '{entry}' is duplicated.");
                continue;
            }

            if (!trustedProxyPolicy.IsValidEntry(entry))
            {
                failures.Add($"{prefix} '{entry}' must be an exact IPv4/IPv6 address or CIDR range.");
            }
        }
    }

    private static void ValidateAdmin(
        List<string> failures,
        ProxyAdminOptions options,
        Func<string, string?> readEnvironmentVariable,
        IProxyAdminUrlPolicy adminUrlPolicy)
    {
        if (options.RecentAuditCapacity is < MinimumAuditCapacity or > MaximumAuditCapacity)
        {
            failures.Add($"Proxy admin setting RecentAuditCapacity must be between {MinimumAuditCapacity} and {MaximumAuditCapacity}.");
        }

        var tokenResolution = ProxyAdminSecurityTokenPolicy.Resolve(options, readEnvironmentVariable);
        if (options.RequireAuthentication && string.IsNullOrEmpty(tokenResolution.Token))
        {
            failures.Add(
                "Proxy admin RequireAuthentication is true, but no token was provided by Admin:Token "
                + $"or environment variable '{tokenResolution.TokenEnvironmentVariable}'.");
        }

        if (!string.IsNullOrEmpty(options.Token) && ContainsControlCharacter(options.Token))
        {
            failures.Add("Proxy admin Token must not contain control characters.");
        }

        if (ContainsControlCharacter(tokenResolution.TokenEnvironmentVariable))
        {
            failures.Add("Proxy admin TokenEnvironmentVariable must not contain control characters.");
        }

        var urls = ProxyAdminSecurityTokenPolicy.NormalizeUrls(options.Urls);
        for (var index = 0; index < urls.Count; index++)
        {
            var url = urls[index];
            var prefix = $"Proxy:Admin:Urls:{index}";
            if (!adminUrlPolicy.IsValid(url))
            {
                failures.Add($"{prefix} '{url}' must be an absolute http or https URL.");
            }
        }

        if (urls.Any(adminUrlPolicy.IsNonLocal)
            && !ProxyAdminSecurityTokenPolicy.IsAuthenticationEnabled(options, readEnvironmentVariable))
        {
            failures.Add("Proxy admin Urls includes a non-local bind address, so Admin:RequireAuthentication must be true with a configured token.");
        }
    }
}
