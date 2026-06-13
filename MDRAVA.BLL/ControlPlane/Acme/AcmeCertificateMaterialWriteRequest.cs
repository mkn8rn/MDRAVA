namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed record AcmeCertificateMaterialWriteRequest
{
    public AcmeCertificateMaterialWriteRequest(
        string StoragePath,
        string CertificateId,
        IReadOnlyList<string> Domains,
        string DataDirectory,
        DateTimeOffset WrittenAtUtc,
        byte[] PfxBytes)
    {
        ArgumentNullException.ThrowIfNull(Domains);
        ArgumentNullException.ThrowIfNull(PfxBytes);

        this.StoragePath = StoragePath;
        this.CertificateId = CertificateId;
        this.Domains = AcmeList.Copy(Domains);
        this.DataDirectory = DataDirectory;
        this.WrittenAtUtc = WrittenAtUtc;
        this.PfxBytes = PfxBytes.ToArray();
    }

    public string StoragePath { get; }

    public string CertificateId { get; }

    public IReadOnlyList<string> Domains { get; }

    public string DataDirectory { get; }

    public DateTimeOffset WrittenAtUtc { get; }

    public byte[] PfxBytes { get; }
}
