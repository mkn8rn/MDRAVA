namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed record AcmeCertificateLifecycleStatus(
    string CertificateId,
    bool Enabled,
    IReadOnlyList<string> Domains,
    bool Active,
    string Source,
    DateTimeOffset? NotBeforeUtc,
    DateTimeOffset? NotAfterUtc,
    DateTimeOffset? RenewalDueAtUtc,
    DateTimeOffset? LastAttemptAtUtc,
    DateTimeOffset? LastSucceededAtUtc,
    DateTimeOffset? LastFailedAtUtc,
    DateTimeOffset? NextAttemptNotBeforeUtc,
    string LastResult,
    string? ErrorSummary)
{
    public static AcmeCertificateLifecycleStatus FromConfiguredCertificate(
        ProxyAcmeConfiguredCertificateStatus certificate,
        ProxyAcmeRuntimeCertificateStatus? runtimeCertificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        var active = runtimeCertificate is not null
            && string.Equals(runtimeCertificate.Source, "acme", StringComparison.OrdinalIgnoreCase);
        return new AcmeCertificateLifecycleStatus(
            certificate.Id,
            certificate.Enabled,
            certificate.Domains,
            active,
            active ? "acme" : "none",
            active ? runtimeCertificate!.NotBeforeUtc : null,
            active ? runtimeCertificate!.NotAfterUtc : null,
            active ? runtimeCertificate!.NotAfterUtc.AddDays(-certificate.RenewBeforeDays) : null,
            LastAttemptAtUtc: null,
            LastSucceededAtUtc: null,
            LastFailedAtUtc: null,
            NextAttemptNotBeforeUtc: null,
            active ? "loaded" : "inactive",
            ErrorSummary: null);
    }
}
