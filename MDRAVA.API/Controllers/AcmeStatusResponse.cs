using MDRAVA.BLL.ControlPlane.Acme;

namespace MDRAVA.API.Controllers;

public sealed record AcmeStatusResponse(
    bool Enabled,
    string DirectoryUrl,
    bool UseStaging,
    IReadOnlyList<AcmeCertificateLifecycleStatus> Certificates)
{
    public static AcmeStatusResponse FromStatus(AcmeStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return new AcmeStatusResponse(
            Enabled: status.Enabled,
            DirectoryUrl: status.DirectoryUrl,
            UseStaging: status.UseStaging,
            Certificates: status.Certificates);
    }
}
