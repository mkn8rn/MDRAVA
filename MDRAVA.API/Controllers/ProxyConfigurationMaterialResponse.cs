using BusinessRuntimeAcmeCertificateOptions = MDRAVA.BLL.Configuration.RuntimeAcmeCertificateOptions;
using BusinessRuntimeAcmeOptions = MDRAVA.BLL.Configuration.RuntimeAcmeOptions;
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
    public static RuntimeAcmeResponse FromOptions(BusinessRuntimeAcmeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new RuntimeAcmeResponse(
            options.Enabled,
            options.UseStaging,
            options.DirectoryUrl,
            options.ContactEmails.ToArray(),
            options.TermsAccepted,
            options.StoragePath,
            options.RenewBeforeDays,
            options.CheckIntervalMinutes,
            options.RetryAfterMinutes,
            options.Certificates.Select(RuntimeAcmeCertificateResponse.FromOptions).ToArray());
    }
}

public sealed record RuntimeAcmeCertificateResponse(
    string Id,
    bool Enabled,
    IReadOnlyList<string> Domains,
    int RenewBeforeDays)
{
    public static RuntimeAcmeCertificateResponse FromOptions(BusinessRuntimeAcmeCertificateOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new RuntimeAcmeCertificateResponse(
            options.Id,
            options.Enabled,
            options.Domains.ToArray(),
            options.RenewBeforeDays);
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
