using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane;

namespace MDRAVA.INF.Acme;

public sealed class AcmeCertificateMaterialWriter : IAcmeCertificateMaterialWriter
{
    public void EnsureLayout(string dataDirectory, string storagePath)
    {
        AcmeCertificateMaterialStore.EnsureLayout(dataDirectory, storagePath);
    }

    public RuntimeCertificate WriteAndLoad(
        RuntimeAcmeOptions acmeOptions,
        RuntimeAcmeCertificateOptions certificateOptions,
        string dataDirectory,
        byte[] pfxBytes)
    {
        return AcmeCertificateMaterialStore.WriteAndLoad(
            acmeOptions,
            certificateOptions,
            dataDirectory,
            pfxBytes);
    }
}
