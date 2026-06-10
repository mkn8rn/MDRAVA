using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Acme;

namespace MDRAVA.INF.Acme;

public sealed class AcmeCertificateMaterialWriter : IAcmeCertificateMaterialWriter
{
    public void EnsureLayout(string dataDirectory, string storagePath)
    {
        AcmeCertificateMaterialStore.EnsureLayout(dataDirectory, storagePath);
    }

    public RuntimeCertificate WriteAndLoad(AcmeCertificateMaterialWriteRequest request)
    {
        return AcmeCertificateMaterialStore.WriteAndLoad(request);
    }
}
