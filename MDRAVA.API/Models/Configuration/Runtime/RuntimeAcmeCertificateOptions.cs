namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeAcmeCertificateOptions(
    string Id,
    bool Enabled,
    IReadOnlyList<string> Domains,
    int RenewBeforeDays);
