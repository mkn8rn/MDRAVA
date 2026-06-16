using BusinessRuntimeAcmeCertificateProjection = MDRAVA.BLL.Configuration.RuntimeAcmeCertificateProjection;
using BusinessRuntimeAcmeProjection = MDRAVA.BLL.Configuration.RuntimeAcmeProjection;
using BusinessRuntimeCertificateProjection = MDRAVA.BLL.Configuration.RuntimeCertificateProjection;

namespace MDRAVA.API.Controllers;

public sealed record RuntimeAcmeResponse
{
    public RuntimeAcmeResponse(
        bool enabled,
        bool useStaging,
        string directoryUrl,
        IReadOnlyList<string> contactEmails,
        bool termsAccepted,
        string storagePath,
        int renewBeforeDays,
        int checkIntervalMinutes,
        int retryAfterMinutes,
        IReadOnlyList<RuntimeAcmeCertificateResponse> certificates)
    {
        Enabled = enabled;
        UseStaging = useStaging;
        DirectoryUrl = directoryUrl;
        ContactEmails = ApiResponseList.Copy(contactEmails);
        TermsAccepted = termsAccepted;
        StoragePath = storagePath;
        RenewBeforeDays = renewBeforeDays;
        CheckIntervalMinutes = checkIntervalMinutes;
        RetryAfterMinutes = retryAfterMinutes;
        Certificates = ApiResponseList.Copy(certificates);
    }

    public bool Enabled { get; }

    public bool UseStaging { get; }

    public string DirectoryUrl { get; }

    public IReadOnlyList<string> ContactEmails { get; }

    public bool TermsAccepted { get; }

    public string StoragePath { get; }

    public int RenewBeforeDays { get; }

    public int CheckIntervalMinutes { get; }

    public int RetryAfterMinutes { get; }

    public IReadOnlyList<RuntimeAcmeCertificateResponse> Certificates { get; }

    public static RuntimeAcmeResponse FromProjection(BusinessRuntimeAcmeProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeAcmeResponse(
            enabled: projection.Enabled,
            useStaging: projection.UseStaging,
            directoryUrl: projection.DirectoryUrl,
            contactEmails: projection.ContactEmails,
            termsAccepted: projection.TermsAccepted,
            storagePath: projection.StoragePath,
            renewBeforeDays: projection.RenewBeforeDays,
            checkIntervalMinutes: projection.CheckIntervalMinutes,
            retryAfterMinutes: projection.RetryAfterMinutes,
            certificates: ApiResponseList.Copy(projection.Certificates.Select(RuntimeAcmeCertificateResponse.FromProjection)));
    }
}

public sealed record RuntimeAcmeCertificateResponse
{
    public RuntimeAcmeCertificateResponse(
        string id,
        bool enabled,
        IReadOnlyList<string> domains,
        int renewBeforeDays)
    {
        Id = id;
        Enabled = enabled;
        Domains = ApiResponseList.Copy(domains);
        RenewBeforeDays = renewBeforeDays;
    }

    public string Id { get; }

    public bool Enabled { get; }

    public IReadOnlyList<string> Domains { get; }

    public int RenewBeforeDays { get; }

    public static RuntimeAcmeCertificateResponse FromProjection(BusinessRuntimeAcmeCertificateProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeAcmeCertificateResponse(
            id: projection.Id,
            enabled: projection.Enabled,
            domains: projection.Domains,
            renewBeforeDays: projection.RenewBeforeDays);
    }
}

public sealed record RuntimeCertificateResponse
{
    public RuntimeCertificateResponse(
        string id,
        string path,
        string format,
        string source,
        IReadOnlyList<string> domains,
        bool hasConfiguredPassword,
        string? subject,
        string? thumbprint,
        DateTime notBefore,
        DateTime notAfter)
    {
        Id = id;
        Path = path;
        Format = format;
        Source = source;
        Domains = ApiResponseList.Copy(domains);
        HasConfiguredPassword = hasConfiguredPassword;
        Subject = subject;
        Thumbprint = thumbprint;
        NotBefore = notBefore;
        NotAfter = notAfter;
    }

    public string Id { get; }

    public string Path { get; }

    public string Format { get; }

    public string Source { get; }

    public IReadOnlyList<string> Domains { get; }

    public bool HasConfiguredPassword { get; }

    public string? Subject { get; }

    public string? Thumbprint { get; }

    public DateTime NotBefore { get; }

    public DateTime NotAfter { get; }

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
            id: certificate.Id,
            path: certificate.Path,
            format: certificate.Format,
            source: certificate.Source,
            domains: certificate.Domains,
            hasConfiguredPassword: certificate.HasConfiguredPassword,
            subject: certificate.Subject,
            thumbprint: certificate.Thumbprint,
            notBefore: certificate.NotBefore,
            notAfter: certificate.NotAfter);
    }
}
