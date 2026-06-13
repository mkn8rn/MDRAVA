namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed record AcmeStatus(
    bool Enabled,
    string DirectoryUrl,
    bool UseStaging,
    IReadOnlyList<AcmeCertificateLifecycleStatus> Certificates)
{
    public static AcmeStatus FromSnapshot(
        ProxyAcmeStatusSnapshot snapshot,
        IReadOnlyList<AcmeCertificateLifecycleStatus> certificates)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(certificates);

        return new AcmeStatus(
            snapshot.Enabled,
            snapshot.DirectoryUrl,
            snapshot.UseStaging,
            certificates);
    }
}
