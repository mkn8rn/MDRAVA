namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeAcmeOptions
{
    public RuntimeAcmeOptions(
        bool Enabled,
        bool UseStaging,
        string DirectoryUrl,
        IReadOnlyList<string> ContactEmails,
        bool TermsAccepted,
        string StoragePath,
        int RenewBeforeDays,
        int CheckIntervalMinutes,
        int RetryAfterMinutes,
        IReadOnlyList<RuntimeAcmeCertificateOptions> Certificates)
    {
        this.Enabled = Enabled;
        this.UseStaging = UseStaging;
        this.DirectoryUrl = DirectoryUrl;
        this.ContactEmails = RuntimeList.Copy(ContactEmails);
        this.TermsAccepted = TermsAccepted;
        this.StoragePath = StoragePath;
        this.RenewBeforeDays = RenewBeforeDays;
        this.CheckIntervalMinutes = CheckIntervalMinutes;
        this.RetryAfterMinutes = RetryAfterMinutes;
        this.Certificates = RuntimeList.Copy(Certificates);
    }

    public bool Enabled { get; init; }

    public bool UseStaging { get; init; }

    public string DirectoryUrl { get; init; }

    public IReadOnlyList<string> ContactEmails { get; }

    public bool TermsAccepted { get; init; }

    public string StoragePath { get; init; }

    public int RenewBeforeDays { get; init; }

    public int CheckIntervalMinutes { get; init; }

    public int RetryAfterMinutes { get; init; }

    public IReadOnlyList<RuntimeAcmeCertificateOptions> Certificates { get; }
}
