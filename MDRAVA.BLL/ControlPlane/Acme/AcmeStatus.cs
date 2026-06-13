namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed record AcmeStatus
{
    public AcmeStatus(
        bool Enabled,
        string DirectoryUrl,
        bool UseStaging,
        IReadOnlyList<AcmeCertificateLifecycleStatus> Certificates)
    {
        ArgumentNullException.ThrowIfNull(Certificates);

        this.Enabled = Enabled;
        this.DirectoryUrl = DirectoryUrl;
        this.UseStaging = UseStaging;
        this.Certificates = AcmeList.Copy(Certificates);
    }

    public bool Enabled { get; }

    public string DirectoryUrl { get; }

    public bool UseStaging { get; }

    public IReadOnlyList<AcmeCertificateLifecycleStatus> Certificates { get; }

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
