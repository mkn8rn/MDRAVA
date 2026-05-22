namespace MDRAVA.BLL.ControlPlane;

public sealed record AcmeStatusResponse(
    bool Enabled,
    string DirectoryUrl,
    bool UseStaging,
    IReadOnlyList<AcmeCertificateLifecycleStatus> Certificates);
