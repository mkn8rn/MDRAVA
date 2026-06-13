using BusinessAcmeCertificateLifecycleStatus = MDRAVA.BLL.ControlPlane.Acme.AcmeCertificateLifecycleStatus;
using BusinessAcmeStatus = MDRAVA.BLL.ControlPlane.Acme.AcmeStatus;

namespace MDRAVA.API.Controllers;

public sealed record AcmeStatusResponse(
    bool Enabled,
    string DirectoryUrl,
    bool UseStaging,
    IReadOnlyList<AcmeCertificateLifecycleStatusResponse> Certificates)
{
    public static AcmeStatusResponse FromStatus(BusinessAcmeStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return new AcmeStatusResponse(
            Enabled: status.Enabled,
            DirectoryUrl: status.DirectoryUrl,
            UseStaging: status.UseStaging,
            Certificates: AcmeCertificateLifecycleStatusResponse.FromStatuses(status.Certificates));
    }
}

public sealed record AcmeCertificateLifecycleStatusResponse(
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
    public static IReadOnlyList<AcmeCertificateLifecycleStatusResponse> FromStatuses(
        IReadOnlyList<BusinessAcmeCertificateLifecycleStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(statuses);

        return statuses.Select(FromStatus).ToArray();
    }

    private static AcmeCertificateLifecycleStatusResponse FromStatus(
        BusinessAcmeCertificateLifecycleStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return new AcmeCertificateLifecycleStatusResponse(
            status.CertificateId,
            status.Enabled,
            status.Domains.ToArray(),
            status.Active,
            status.Source,
            status.NotBeforeUtc,
            status.NotAfterUtc,
            status.RenewalDueAtUtc,
            status.LastAttemptAtUtc,
            status.LastSucceededAtUtc,
            status.LastFailedAtUtc,
            status.NextAttemptNotBeforeUtc,
            status.LastResult,
            status.ErrorSummary);
    }
}
