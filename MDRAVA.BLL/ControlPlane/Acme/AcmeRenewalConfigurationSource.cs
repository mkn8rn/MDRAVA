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

public static class AcmeRenewalConfigurationInputMapper
{
    public static AcmeRenewalConfigurationInput FromRuntimeConfiguration(
        RuntimeAcmeOptions acme,
        IReadOnlyDictionary<string, RuntimeCertificate> runtimeCertificates)
    {
        return new AcmeRenewalConfigurationInput(
            acme.Enabled,
            acme.StoragePath,
            acme.DirectoryUrl,
            acme.ContactEmails,
            acme.TermsAccepted,
            acme.RetryAfterMinutes,
            acme.Certificates
                .Select(certificate => new AcmeRenewalCertificateInput(
                    certificate.Id,
                    certificate.Enabled,
                    certificate.Domains,
                    certificate.RenewBeforeDays,
                    ReadActiveAcmeCertificate(runtimeCertificates, certificate.Id)))
                .ToArray());
    }

    private static RuntimeCertificate? ReadActiveAcmeCertificate(
        IReadOnlyDictionary<string, RuntimeCertificate> runtimeCertificates,
        string certificateId)
    {
        return runtimeCertificates.TryGetValue(certificateId, out var certificate)
            && string.Equals(certificate.Source, "acme", StringComparison.OrdinalIgnoreCase)
            ? certificate
            : null;
    }
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

        return AcmeRenewalConfigurationInputMapper.FromRuntimeConfiguration(
            snapshot.Acme,
            snapshot.Certificates);
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
