using BusinessAcmeCertificateLifecycleStatus = MDRAVA.BLL.ControlPlane.Acme.AcmeCertificateLifecycleStatus;
using BusinessAcmeStatus = MDRAVA.BLL.ControlPlane.Acme.AcmeStatus;

namespace MDRAVA.API.Controllers;

public sealed record AcmeStatusResponse
{
    public AcmeStatusResponse(
        bool enabled,
        string directoryUrl,
        bool useStaging,
        IReadOnlyList<AcmeCertificateLifecycleStatusResponse> certificates)
    {
        Enabled = enabled;
        DirectoryUrl = directoryUrl;
        UseStaging = useStaging;
        Certificates = ApiResponseList.Copy(certificates);
    }

    public bool Enabled { get; }

    public string DirectoryUrl { get; }

    public bool UseStaging { get; }

    public IReadOnlyList<AcmeCertificateLifecycleStatusResponse> Certificates { get; }

    public static AcmeStatusResponse FromStatus(BusinessAcmeStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return new AcmeStatusResponse(
            enabled: status.Enabled,
            directoryUrl: status.DirectoryUrl,
            useStaging: status.UseStaging,
            certificates: AcmeCertificateLifecycleStatusResponse.FromStatuses(status.Certificates));
    }
}

public sealed record AcmeCertificateLifecycleStatusResponse
{
    public AcmeCertificateLifecycleStatusResponse(
        string certificateId,
        bool enabled,
        IReadOnlyList<string> domains,
        bool active,
        string source,
        DateTimeOffset? notBeforeUtc,
        DateTimeOffset? notAfterUtc,
        DateTimeOffset? renewalDueAtUtc,
        DateTimeOffset? lastAttemptAtUtc,
        DateTimeOffset? lastSucceededAtUtc,
        DateTimeOffset? lastFailedAtUtc,
        DateTimeOffset? nextAttemptNotBeforeUtc,
        string lastResult,
        string? errorSummary)
    {
        CertificateId = certificateId;
        Enabled = enabled;
        Domains = ApiResponseList.Copy(domains);
        Active = active;
        Source = source;
        NotBeforeUtc = notBeforeUtc;
        NotAfterUtc = notAfterUtc;
        RenewalDueAtUtc = renewalDueAtUtc;
        LastAttemptAtUtc = lastAttemptAtUtc;
        LastSucceededAtUtc = lastSucceededAtUtc;
        LastFailedAtUtc = lastFailedAtUtc;
        NextAttemptNotBeforeUtc = nextAttemptNotBeforeUtc;
        LastResult = lastResult;
        ErrorSummary = errorSummary;
    }

    public string CertificateId { get; }

    public bool Enabled { get; }

    public IReadOnlyList<string> Domains { get; }

    public bool Active { get; }

    public string Source { get; }

    public DateTimeOffset? NotBeforeUtc { get; }

    public DateTimeOffset? NotAfterUtc { get; }

    public DateTimeOffset? RenewalDueAtUtc { get; }

    public DateTimeOffset? LastAttemptAtUtc { get; }

    public DateTimeOffset? LastSucceededAtUtc { get; }

    public DateTimeOffset? LastFailedAtUtc { get; }

    public DateTimeOffset? NextAttemptNotBeforeUtc { get; }

    public string LastResult { get; }

    public string? ErrorSummary { get; }

    public static IReadOnlyList<AcmeCertificateLifecycleStatusResponse> FromStatuses(
        IReadOnlyList<BusinessAcmeCertificateLifecycleStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(statuses);

        return ApiResponseList.Copy(statuses.Select(FromStatus));
    }

    private static AcmeCertificateLifecycleStatusResponse FromStatus(
        BusinessAcmeCertificateLifecycleStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return new AcmeCertificateLifecycleStatusResponse(
            certificateId: status.CertificateId,
            enabled: status.Enabled,
            domains: status.Domains,
            active: status.Active,
            source: status.Source,
            notBeforeUtc: status.NotBeforeUtc,
            notAfterUtc: status.NotAfterUtc,
            renewalDueAtUtc: status.RenewalDueAtUtc,
            lastAttemptAtUtc: status.LastAttemptAtUtc,
            lastSucceededAtUtc: status.LastSucceededAtUtc,
            lastFailedAtUtc: status.LastFailedAtUtc,
            nextAttemptNotBeforeUtc: status.NextAttemptNotBeforeUtc,
            lastResult: status.LastResult,
            errorSummary: status.ErrorSummary);
    }
}
