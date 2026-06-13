namespace MDRAVA.BLL.Configuration;

public static partial class ProxyOperationalOptionsValidationRules
{
    private static void ValidateAcme(
        List<string> failures,
        ProxyAcmeOptions options,
        IReadOnlyList<CertificateOptions> manualCertificates,
        IProxyRelativeStoragePathPolicy relativeStoragePathPolicy,
        IProxyUrlSyntaxPolicy urlSyntaxPolicy)
    {
        if (options.RenewBeforeDays is < 1 or > 365)
        {
            failures.Add("Proxy ACME setting RenewBeforeDays must be between 1 and 365.");
        }

        if (options.CheckIntervalMinutes is < 5 or > 1440)
        {
            failures.Add("Proxy ACME setting CheckIntervalMinutes must be between 5 and 1440.");
        }

        if (options.RetryAfterMinutes is < 5 or > 1440)
        {
            failures.Add("Proxy ACME setting RetryAfterMinutes must be between 5 and 1440.");
        }

        if (!relativeStoragePathPolicy.IsSafeRelativePath(options.StoragePath))
        {
            failures.Add("Proxy ACME setting StoragePath must be a relative path under the data-directory certs folder.");
        }

        var directoryUrl = ProxyAcmeDirectoryPolicy.ResolveDirectoryUrl(options);
        if (!urlSyntaxPolicy.IsAbsoluteHttpsUrl(directoryUrl))
        {
            failures.Add("Proxy ACME DirectoryUrl must be an absolute https URL.");
        }

        if (options.Enabled && options.Certificates.Any(static certificate => certificate.Enabled) && !options.TermsAccepted)
        {
            failures.Add("Proxy ACME TermsAccepted must be true before ACME-managed certificates can be issued or renewed.");
        }

        foreach (var contact in options.ContactEmails)
        {
            if (string.IsNullOrWhiteSpace(contact) || ContainsControlCharacter(contact) || !contact.Contains('@', StringComparison.Ordinal))
            {
                failures.Add("Proxy ACME ContactEmails entries must be non-empty email-like values without control characters.");
            }
        }

        HashSet<string> ids = manualCertificates
            .Select(static certificate => certificate.Id)
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < options.Certificates.Count; index++)
        {
            var certificate = options.Certificates[index];
            var prefix = $"Proxy:Acme:Certificates:{index}";
            if (string.IsNullOrWhiteSpace(certificate.Id))
            {
                failures.Add($"{prefix}:Id is required.");
            }
            else if (!IsSafeStorageSegment(certificate.Id))
            {
                failures.Add($"{prefix}:Id must contain only letters, digits, dot, dash, or underscore.");
            }
            else if (!ids.Add(certificate.Id))
            {
                failures.Add($"{prefix}:Id '{certificate.Id}' duplicates another manual or ACME certificate id.");
            }

            if (certificate.RenewBeforeDays is < 1 or > 365)
            {
                failures.Add($"{prefix}:RenewBeforeDays must be between 1 and 365.");
            }

            if (certificate.Enabled && certificate.Domains.Count == 0)
            {
                failures.Add($"{prefix}:Domains must contain at least one DNS name when the ACME certificate is enabled.");
            }

            HashSet<string> domains = new(StringComparer.OrdinalIgnoreCase);
            for (var domainIndex = 0; domainIndex < certificate.Domains.Count; domainIndex++)
            {
                var domain = certificate.Domains[domainIndex];
                var domainPrefix = $"{prefix}:Domains:{domainIndex}";
                if (!IsValidAcmeDomain(domain))
                {
                    failures.Add($"{domainPrefix} '{domain}' must be a non-wildcard DNS name without control characters.");
                }
                else if (!domains.Add(domain.Trim()))
                {
                    failures.Add($"{domainPrefix} '{domain}' is duplicated.");
                }
            }
        }
    }

    private static bool IsSafeStorageSegment(string value)
    {
        if (value.Length is 0 or > 128)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character)
                && character is not '.' and not '-' and not '_')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidAcmeDomain(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Length is > 253
            || trimmed.StartsWith("*.", StringComparison.Ordinal)
            || trimmed.Contains('/', StringComparison.Ordinal)
            || trimmed.Contains('\\', StringComparison.Ordinal)
            || ContainsControlCharacter(trimmed))
        {
            return false;
        }

        return trimmed.Contains('.', StringComparison.Ordinal);
    }
}
