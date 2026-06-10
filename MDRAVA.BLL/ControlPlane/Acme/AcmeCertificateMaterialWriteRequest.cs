namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed record AcmeCertificateMaterialWriteRequest(
    string StoragePath,
    string CertificateId,
    IReadOnlyList<string> Domains,
    string DataDirectory,
    DateTimeOffset WrittenAtUtc,
    byte[] PfxBytes);
