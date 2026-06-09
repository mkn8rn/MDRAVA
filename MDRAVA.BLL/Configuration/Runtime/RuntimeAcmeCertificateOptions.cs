namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeAcmeCertificateOptions(
    string Id,
    bool Enabled,
    IReadOnlyList<string> Domains,
    int RenewBeforeDays);
