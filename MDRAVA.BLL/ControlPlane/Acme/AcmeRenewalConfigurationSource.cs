using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed record AcmeRenewalConfigurationInput
{
    public AcmeRenewalConfigurationInput(
        bool Enabled,
        string StoragePath,
        string DirectoryUrl,
        IReadOnlyList<string> ContactEmails,
        bool TermsAccepted,
        int RetryAfterMinutes,
        IReadOnlyList<AcmeRenewalCertificateInput> Certificates)
    {
        ArgumentNullException.ThrowIfNull(ContactEmails);
        ArgumentNullException.ThrowIfNull(Certificates);

        this.Enabled = Enabled;
        this.StoragePath = StoragePath;
        this.DirectoryUrl = DirectoryUrl;
        this.ContactEmails = AcmeList.Copy(ContactEmails);
        this.TermsAccepted = TermsAccepted;
        this.RetryAfterMinutes = RetryAfterMinutes;
        this.Certificates = AcmeList.Copy(Certificates);
    }

    public bool Enabled { get; }

    public string StoragePath { get; }

    public string DirectoryUrl { get; }

    public IReadOnlyList<string> ContactEmails { get; }

    public bool TermsAccepted { get; }

    public int RetryAfterMinutes { get; }

    public IReadOnlyList<AcmeRenewalCertificateInput> Certificates { get; }
}

public sealed record AcmeRenewalCertificateInput
{
    public AcmeRenewalCertificateInput(
        string Id,
        bool Enabled,
        IReadOnlyList<string> Domains,
        int RenewBeforeDays,
        AcmeRenewalActiveCertificate? ActiveCertificate)
    {
        ArgumentNullException.ThrowIfNull(Domains);

        this.Id = Id;
        this.Enabled = Enabled;
        this.Domains = AcmeList.Copy(Domains);
        this.RenewBeforeDays = RenewBeforeDays;
        this.ActiveCertificate = ActiveCertificate;
    }

    public string Id { get; }

    public bool Enabled { get; }

    public IReadOnlyList<string> Domains { get; }

    public int RenewBeforeDays { get; }

    public AcmeRenewalActiveCertificate? ActiveCertificate { get; }
}

public sealed record AcmeRenewalActiveCertificate(
    DateTimeOffset NotBeforeUtc,
    DateTimeOffset NotAfterUtc);

public sealed record AcmeRenewalConfigurationSourceSet
{
    public AcmeRenewalConfigurationSourceSet(
        bool Enabled,
        string StoragePath,
        string DirectoryUrl,
        IReadOnlyList<string> ContactEmails,
        bool TermsAccepted,
        int RetryAfterMinutes,
        IReadOnlyList<AcmeRenewalCertificateSource> Certificates)
    {
        ArgumentNullException.ThrowIfNull(ContactEmails);
        ArgumentNullException.ThrowIfNull(Certificates);

        this.Enabled = Enabled;
        this.StoragePath = StoragePath;
        this.DirectoryUrl = DirectoryUrl;
        this.ContactEmails = AcmeList.Copy(ContactEmails);
        this.TermsAccepted = TermsAccepted;
        this.RetryAfterMinutes = RetryAfterMinutes;
        this.Certificates = AcmeList.Copy(Certificates);
    }

    public bool Enabled { get; }

    public string StoragePath { get; }

    public string DirectoryUrl { get; }

    public IReadOnlyList<string> ContactEmails { get; }

    public bool TermsAccepted { get; }

    public int RetryAfterMinutes { get; }

    public IReadOnlyList<AcmeRenewalCertificateSource> Certificates { get; }
}

public sealed record AcmeRenewalCertificateSource
{
    public AcmeRenewalCertificateSource(
        string Id,
        bool Enabled,
        IReadOnlyList<string> Domains,
        int RenewBeforeDays,
        AcmeRenewalActiveCertificate? ActiveCertificate)
    {
        ArgumentNullException.ThrowIfNull(Domains);

        this.Id = Id;
        this.Enabled = Enabled;
        this.Domains = AcmeList.Copy(Domains);
        this.RenewBeforeDays = RenewBeforeDays;
        this.ActiveCertificate = ActiveCertificate;
    }

    public string Id { get; }

    public bool Enabled { get; }

    public IReadOnlyList<string> Domains { get; }

    public int RenewBeforeDays { get; }

    public AcmeRenewalActiveCertificate? ActiveCertificate { get; }
}

public interface IAcmeRenewalConfigurationSource
{
    AcmeRenewalConfigurationInputReadResult ReadInput();
}

public abstract record AcmeRenewalConfigurationInputReadResult
{
    private AcmeRenewalConfigurationInputReadResult()
    {
    }

    public static AcmeRenewalConfigurationInputReadResult MissingConfiguration { get; } =
        new MissingConfigurationResult();

    public static AcmeRenewalConfigurationInputReadResult Available(AcmeRenewalConfigurationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return new AvailableResult(input);
    }

    public sealed record AvailableResult : AcmeRenewalConfigurationInputReadResult
    {
        public AvailableResult(AcmeRenewalConfigurationInput input)
        {
            ArgumentNullException.ThrowIfNull(input);

            Input = input;
        }

        public AcmeRenewalConfigurationInput Input { get; }
    }

    public sealed record MissingConfigurationResult : AcmeRenewalConfigurationInputReadResult;
}

public interface IAcmeCertificateActivator
{
    void Activate(RuntimeCertificate certificate);
}

public static class AcmeRenewalConfigurationInputMapper
{
    public static AcmeRenewalConfigurationInput FromSources(
        AcmeRenewalConfigurationSourceSet source)
    {
        return new AcmeRenewalConfigurationInput(
            source.Enabled,
            source.StoragePath,
            source.DirectoryUrl,
            source.ContactEmails,
            source.TermsAccepted,
            source.RetryAfterMinutes,
            source.Certificates
                .Select(static certificate => new AcmeRenewalCertificateInput(
                    certificate.Id,
                    certificate.Enabled,
                    certificate.Domains,
                    certificate.RenewBeforeDays,
                    certificate.ActiveCertificate))
                .ToArray());
    }
}
