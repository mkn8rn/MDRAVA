using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.INF.Acme;

public static class AcmeRenewalConfigurationSourceMapper
{
    public static AcmeRenewalConfigurationSourceSet FromRuntimeConfiguration(
        RuntimeAcmeOptions acme,
        IReadOnlyDictionary<string, RuntimeCertificate> runtimeCertificates)
    {
        return new AcmeRenewalConfigurationSourceSet(
            acme.Enabled,
            acme.StoragePath,
            acme.DirectoryUrl,
            acme.ContactEmails,
            acme.TermsAccepted,
            acme.RetryAfterMinutes,
            acme.Certificates
                .Select(certificate => new AcmeRenewalCertificateSource(
                    certificate.Id,
                    certificate.Enabled,
                    certificate.Domains,
                    certificate.RenewBeforeDays,
                    ReadActiveAcmeCertificate(runtimeCertificates, certificate.Id)))
                .ToArray());
    }

    private static AcmeRenewalActiveCertificate? ReadActiveAcmeCertificate(
        IReadOnlyDictionary<string, RuntimeCertificate> runtimeCertificates,
        string certificateId)
    {
        if (!runtimeCertificates.TryGetValue(certificateId, out var certificate)
            || !string.Equals(certificate.Source, "acme", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new AcmeRenewalActiveCertificate(
            certificate.Certificate.NotBefore.ToUniversalTime(),
            certificate.Certificate.NotAfter.ToUniversalTime());
    }
}

public sealed class ProxyConfigurationAcmeRenewalConfigurationSource : IAcmeRenewalConfigurationSource
{
    private readonly IProxyActiveConfigurationSnapshotReader _configurationStore;

    public ProxyConfigurationAcmeRenewalConfigurationSource(IProxyActiveConfigurationSnapshotReader configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public AcmeRenewalConfigurationInputReadResult ReadInput()
    {
        var snapshotResult = _configurationStore.ReadSnapshot();
        if (snapshotResult is not ProxyConfigurationSnapshotReadResult.AvailableResult available)
        {
            return AcmeRenewalConfigurationInputReadResult.MissingConfiguration;
        }

        var snapshot = available.Snapshot;
        return AcmeRenewalConfigurationInputReadResult.Available(
            AcmeRenewalConfigurationInputMapper.FromSources(
                AcmeRenewalConfigurationSourceMapper.FromRuntimeConfiguration(
                    snapshot.Acme,
                    snapshot.Certificates)));
    }
}
