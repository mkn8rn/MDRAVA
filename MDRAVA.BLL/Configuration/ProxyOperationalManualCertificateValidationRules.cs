namespace MDRAVA.BLL.Configuration;

public static partial class ProxyOperationalOptionsValidationRules
{
    private static void ValidateCertificates(List<string> failures, IReadOnlyList<CertificateOptions> certificates)
    {
        HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < certificates.Count; index++)
        {
            var certificate = certificates[index];
            var prefix = $"Proxy:Certificates:{index}";

            if (string.IsNullOrWhiteSpace(certificate.Id))
            {
                failures.Add($"{prefix}:Id is required.");
            }
            else if (!ids.Add(certificate.Id))
            {
                failures.Add($"{prefix}:Id '{certificate.Id}' is duplicated.");
            }

            if (!string.Equals(certificate.Format, "pfx", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"{prefix}:Format must be 'pfx' for Phase 5.");
            }

            if (string.IsNullOrWhiteSpace(certificate.Path))
            {
                failures.Add($"{prefix}:Path is required.");
            }

            if (!string.IsNullOrEmpty(certificate.Password)
                && !string.IsNullOrWhiteSpace(certificate.PasswordEnvironmentVariable))
            {
                failures.Add($"{prefix} must not set both Password and PasswordEnvironmentVariable.");
            }
        }
    }
}
