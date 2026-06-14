using BusinessRuntimeAcmeCertificateProjection = MDRAVA.BLL.Configuration.RuntimeAcmeCertificateProjection;
using BusinessRuntimeAcmeProjection = MDRAVA.BLL.Configuration.RuntimeAcmeProjection;
using BusinessRuntimeCertificateProjection = MDRAVA.BLL.Configuration.RuntimeCertificateProjection;

namespace MDRAVA.API.Controllers;

public sealed record RuntimeAcmeResponse(
    bool Enabled,
    bool UseStaging,
    string DirectoryUrl,
    IReadOnlyList<string> ContactEmails,
    bool TermsAccepted,
    string StoragePath,
    int RenewBeforeDays,
    int CheckIntervalMinutes,
    int RetryAfterMinutes,
    IReadOnlyList<RuntimeAcmeCertificateResponse> Certificates)
{
    public static RuntimeAcmeResponse FromProjection(BusinessRuntimeAcmeProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeAcmeResponse(
            projection.Enabled,
            projection.UseStaging,
            projection.DirectoryUrl,
            projection.ContactEmails.ToArray(),
            projection.TermsAccepted,
            projection.StoragePath,
            projection.RenewBeforeDays,
            projection.CheckIntervalMinutes,
            projection.RetryAfterMinutes,
            projection.Certificates.Select(RuntimeAcmeCertificateResponse.FromProjection).ToArray());
    }
}

public sealed record RuntimeAcmeCertificateResponse(
    string Id,
    bool Enabled,
    IReadOnlyList<string> Domains,
    int RenewBeforeDays)
{
    public static RuntimeAcmeCertificateResponse FromProjection(BusinessRuntimeAcmeCertificateProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeAcmeCertificateResponse(
            projection.Id,
            projection.Enabled,
            projection.Domains.ToArray(),
            projection.RenewBeforeDays);
    }
}

public sealed record RuntimeCertificateResponse(
    string Id,
    string Path,
    string Format,
    string Source,
    IReadOnlyList<string> Domains,
    bool HasConfiguredPassword,
    string? Subject,
    string? Thumbprint,
    DateTime NotBefore,
    DateTime NotAfter)
{
    public static IReadOnlyList<RuntimeCertificateResponse> FromCertificates(
        IReadOnlyList<BusinessRuntimeCertificateProjection> certificates)
    {
        ArgumentNullException.ThrowIfNull(certificates);

        return certificates.Select(FromCertificate).ToArray();
    }

    private static RuntimeCertificateResponse FromCertificate(BusinessRuntimeCertificateProjection certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        return new RuntimeCertificateResponse(
            certificate.Id,
            certificate.Path,
            certificate.Format,
            certificate.Source,
            certificate.Domains.ToArray(),
            certificate.HasConfiguredPassword,
            certificate.Subject,
            certificate.Thumbprint,
            certificate.NotBefore,
            certificate.NotAfter);
    }
}
