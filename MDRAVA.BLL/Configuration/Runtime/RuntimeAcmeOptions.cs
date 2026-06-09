namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeAcmeOptions(
    bool Enabled,
    bool UseStaging,
    string DirectoryUrl,
    IReadOnlyList<string> ContactEmails,
    bool TermsAccepted,
    string StoragePath,
    int RenewBeforeDays,
    int CheckIntervalMinutes,
    int RetryAfterMinutes,
    IReadOnlyList<RuntimeAcmeCertificateOptions> Certificates);
