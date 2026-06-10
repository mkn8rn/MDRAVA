using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Acme;

public interface IAcmeCertificateMaterialWriter
{
    void EnsureLayout(string dataDirectory, string storagePath);

    RuntimeCertificate WriteAndLoad(AcmeCertificateMaterialWriteRequest request);
}
