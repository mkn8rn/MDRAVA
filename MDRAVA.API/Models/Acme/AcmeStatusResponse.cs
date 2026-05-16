namespace MDRAVA.API.Models.Acme;

public sealed record AcmeStatusResponse(
    bool Enabled,
    string DirectoryUrl,
    bool UseStaging,
    IReadOnlyList<AcmeCertificateLifecycleStatus> Certificates);
