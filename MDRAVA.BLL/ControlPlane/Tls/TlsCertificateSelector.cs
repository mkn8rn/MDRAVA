using System.Collections.ObjectModel;
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

public sealed record TlsCertificateSelectionInput
{
    public TlsCertificateSelectionInput(
        IEnumerable<KeyValuePair<string, RuntimeCertificate>> certificates,
        string? defaultCertificateId,
        IEnumerable<RuntimeSniCertificateBinding> sniCertificates,
        string? hostName)
    {
        ArgumentNullException.ThrowIfNull(certificates);
        ArgumentNullException.ThrowIfNull(sniCertificates);

        Certificates = new ReadOnlyDictionary<string, RuntimeCertificate>(
            certificates.ToDictionary(
                static certificate => certificate.Key,
                static certificate => certificate.Value,
                StringComparer.OrdinalIgnoreCase));
        DefaultCertificateId = defaultCertificateId;
        SniCertificates = new ReadOnlyCollection<RuntimeSniCertificateBinding>(sniCertificates.ToArray());
        HostName = hostName;
    }

    public IReadOnlyDictionary<string, RuntimeCertificate> Certificates { get; }

    public string? DefaultCertificateId { get; }

    public IReadOnlyList<RuntimeSniCertificateBinding> SniCertificates { get; }

    public string? HostName { get; }
}

public static class TlsCertificateSelectionInputMapper
{
    public static TlsCertificateSelectionInput FromSources(
        IEnumerable<KeyValuePair<string, RuntimeCertificate>> certificates,
        string? defaultCertificateId,
        IEnumerable<RuntimeSniCertificateBinding> sniCertificates,
        string? hostName)
    {
        return new TlsCertificateSelectionInput(
            certificates,
            defaultCertificateId,
            sniCertificates,
            hostName);
    }
}
