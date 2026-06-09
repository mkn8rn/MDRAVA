using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane;

public interface IAcmeCertificateMaterialWriter
{
    void EnsureLayout(string dataDirectory, string storagePath);

    RuntimeCertificate WriteAndLoad(
        RuntimeAcmeOptions acmeOptions,
        RuntimeAcmeCertificateOptions certificateOptions,
        string dataDirectory,
        byte[] pfxBytes);
}
