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
    string? ErrorSummary);
