using System.Security.Cryptography.X509Certificates;
using MDRAVA.API.Proxy.Configuration.Runtime;

namespace MDRAVA.API.Proxy.Tls;

public static class TlsCertificateSelector
{
    public static X509Certificate2? SelectCertificate(
        ProxyConfigurationSnapshot snapshot,
        RuntimeListener listener,
        string? hostName)
    {
        if (!string.IsNullOrWhiteSpace(hostName))
        {
            foreach (var binding in listener.SniCertificates)
            {
                if (string.Equals(binding.HostName, hostName, StringComparison.OrdinalIgnoreCase)
                    && snapshot.Certificates.TryGetValue(binding.CertificateId, out var certificate))
                {
                    return certificate.Certificate;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(listener.DefaultCertificateId)
            && snapshot.Certificates.TryGetValue(listener.DefaultCertificateId, out var defaultCertificate))
        {
            return defaultCertificate.Certificate;
        }

        return null;
    }
}
