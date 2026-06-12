namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed record AcmeStatusResponse(
    bool Enabled,
    string DirectoryUrl,
    bool UseStaging,
    IReadOnlyList<AcmeCertificateLifecycleStatus> Certificates)
{
    public static AcmeStatusResponse FromSnapshot(
        ProxyAcmeStatusSnapshot snapshot,
        IReadOnlyList<AcmeCertificateLifecycleStatus> certificates)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(certificates);

        return new AcmeStatusResponse(
            snapshot.Enabled,
            snapshot.DirectoryUrl,
            snapshot.UseStaging,
            certificates);
    }
}
