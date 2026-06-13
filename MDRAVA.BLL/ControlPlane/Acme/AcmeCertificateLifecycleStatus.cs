namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed record AcmeCertificateLifecycleStatus
{
    private IReadOnlyList<string> _domains = AcmeList.Copy<string>([]);

    public AcmeCertificateLifecycleStatus(
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
        ArgumentNullException.ThrowIfNull(Domains);

        this.CertificateId = CertificateId;
        this.Enabled = Enabled;
        this.Domains = Domains;
        this.Active = Active;
        this.Source = Source;
        this.NotBeforeUtc = NotBeforeUtc;
        this.NotAfterUtc = NotAfterUtc;
        this.RenewalDueAtUtc = RenewalDueAtUtc;
        this.LastAttemptAtUtc = LastAttemptAtUtc;
        this.LastSucceededAtUtc = LastSucceededAtUtc;
        this.LastFailedAtUtc = LastFailedAtUtc;
        this.NextAttemptNotBeforeUtc = NextAttemptNotBeforeUtc;
        this.LastResult = LastResult;
        this.ErrorSummary = ErrorSummary;
    }

    public string CertificateId { get; init; }

    public bool Enabled { get; init; }

    public IReadOnlyList<string> Domains
    {
        get => _domains;
        init => _domains = AcmeList.Copy(value);
    }

    public bool Active { get; init; }

    public string Source { get; init; }

    public DateTimeOffset? NotBeforeUtc { get; init; }

    public DateTimeOffset? NotAfterUtc { get; init; }

    public DateTimeOffset? RenewalDueAtUtc { get; init; }

    public DateTimeOffset? LastAttemptAtUtc { get; init; }

    public DateTimeOffset? LastSucceededAtUtc { get; init; }

    public DateTimeOffset? LastFailedAtUtc { get; init; }

    public DateTimeOffset? NextAttemptNotBeforeUtc { get; init; }

    public string LastResult { get; init; }

    public string? ErrorSummary { get; init; }

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
