using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed record AcmeRenewalConfigurationInput(
    bool Enabled,
    string StoragePath,
    string DirectoryUrl,
    IReadOnlyList<string> ContactEmails,
    bool TermsAccepted,
    int RetryAfterMinutes,
    IReadOnlyList<AcmeRenewalCertificateInput> Certificates);

public sealed record AcmeRenewalCertificateInput(
    string Id,
    bool Enabled,
    IReadOnlyList<string> Domains,
    int RenewBeforeDays,
    RuntimeCertificate? ActiveCertificate);

public interface IAcmeRenewalConfigurationSource
{
    AcmeRenewalConfigurationInput? ReadInput();
}

public interface IAcmeCertificateActivator
{
    void Activate(RuntimeCertificate certificate);
}

public sealed class ProxyConfigurationAcmeRenewalConfigurationSource : IAcmeRenewalConfigurationSource
{
    private readonly IProxyConfigurationStore _configurationStore;

    public ProxyConfigurationAcmeRenewalConfigurationSource(IProxyConfigurationStore configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public AcmeRenewalConfigurationInput? ReadInput()
    {
        if (!_configurationStore.TryGetSnapshot(out var snapshot) || snapshot is null)
        {
            return null;
        }

        return new AcmeRenewalConfigurationInput(
            snapshot.Acme.Enabled,
            snapshot.Acme.StoragePath,
            snapshot.Acme.DirectoryUrl,
            snapshot.Acme.ContactEmails,
            snapshot.Acme.TermsAccepted,
            snapshot.Acme.RetryAfterMinutes,
            snapshot.Acme.Certificates
                .Select(certificate => new AcmeRenewalCertificateInput(
                    certificate.Id,
                    certificate.Enabled,
                    certificate.Domains,
                    certificate.RenewBeforeDays,
                    ReadActiveAcmeCertificate(snapshot, certificate.Id)))
                .ToArray());
    }

    private static RuntimeCertificate? ReadActiveAcmeCertificate(
        ProxyConfigurationSnapshot snapshot,
        string certificateId)
    {
        return snapshot.Certificates.TryGetValue(certificateId, out var certificate)
            && string.Equals(certificate.Source, "acme", StringComparison.OrdinalIgnoreCase)
            ? certificate
            : null;
    }
}

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

        _configurationStore.Replace(snapshot with { Certificates = certificates });
    }
}
