using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.INF.Acme;

public sealed class ProxyConfigurationAcmeCertificateActivator : IAcmeCertificateActivator
{
    private readonly IProxyConfigurationStore _configurationStore;

    public ProxyConfigurationAcmeCertificateActivator(IProxyConfigurationStore configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public void Activate(RuntimeCertificate certificate)
    {
        var snapshot = _configurationStore.Snapshot;
        Dictionary<string, RuntimeCertificate> certificates = new(snapshot.Certificates, StringComparer.OrdinalIgnoreCase)
        {
            [certificate.Id] = certificate
        };

        _configurationStore.Replace(snapshot.WithCertificates(certificates));
    }
}
