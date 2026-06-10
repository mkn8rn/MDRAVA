namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed record AcmeStatusResponse(
    bool Enabled,
    string DirectoryUrl,
    bool UseStaging,
    IReadOnlyList<AcmeCertificateLifecycleStatus> Certificates);
