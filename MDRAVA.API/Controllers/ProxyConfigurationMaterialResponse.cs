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
            ApiResponseList.Copy(projection.ContactEmails),
            projection.TermsAccepted,
            projection.StoragePath,
            projection.RenewBeforeDays,
            projection.CheckIntervalMinutes,
            projection.RetryAfterMinutes,
            ApiResponseList.Copy(projection.Certificates.Select(RuntimeAcmeCertificateResponse.FromProjection)));
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
            ApiResponseList.Copy(projection.Domains),
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

        return ApiResponseList.Copy(certificates.Select(FromCertificate));
    }

    private static RuntimeCertificateResponse FromCertificate(BusinessRuntimeCertificateProjection certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        return new RuntimeCertificateResponse(
            certificate.Id,
            certificate.Path,
            certificate.Format,
            certificate.Source,
            ApiResponseList.Copy(certificate.Domains),
            certificate.HasConfiguredPassword,
            certificate.Subject,
            certificate.Thumbprint,
            certificate.NotBefore,
            certificate.NotAfter);
    }
}
