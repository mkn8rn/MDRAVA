using System.Security.Cryptography.X509Certificates;
using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Tls;

public static class TlsCertificateSelector
{
    public static X509Certificate2? SelectCertificate(TlsCertificateSelectionInput input)
    {
        if (!string.IsNullOrWhiteSpace(input.HostName))
        {
            foreach (var binding in input.SniCertificates)
            {
                if (string.Equals(binding.HostName, input.HostName, StringComparison.OrdinalIgnoreCase)
                    && input.Certificates.TryGetValue(binding.CertificateId, out var certificate))
                {
                    return certificate.Certificate;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(input.DefaultCertificateId)
            && input.Certificates.TryGetValue(input.DefaultCertificateId, out var defaultCertificate))
        {
            return defaultCertificate.Certificate;
        }

        return null;
    }
}

public sealed record TlsCertificateSelectionInput(
    IReadOnlyDictionary<string, RuntimeCertificate> Certificates,
    string? DefaultCertificateId,
    IReadOnlyList<RuntimeSniCertificateBinding> SniCertificates,
    string? HostName);

public static class TlsCertificateSelectionInputMapper
{
    public static TlsCertificateSelectionInput FromRuntimeConfiguration(
        ProxyConfigurationSnapshot snapshot,
        RuntimeListener listener,
        string? hostName)
    {
        return new TlsCertificateSelectionInput(
            snapshot.Certificates,
            listener.DefaultCertificateId,
            listener.SniCertificates,
            hostName);
    }
}
