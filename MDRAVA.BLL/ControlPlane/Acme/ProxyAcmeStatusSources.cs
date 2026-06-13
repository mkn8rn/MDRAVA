using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed class ProxyAcmeCertificateLifecycleStatusSource
    : IProxyAcmeCertificateLifecycleStatusSource
{
    private readonly AcmeCertificateStatusStore _statusStore;

    public ProxyAcmeCertificateLifecycleStatusSource(AcmeCertificateStatusStore statusStore)
    {
        _statusStore = statusStore;
    }

    public IReadOnlyList<AcmeCertificateLifecycleStatus> GetLifecycleStatuses()
    {
        return _statusStore.Snapshot();
    }
}

public sealed class ProxyAcmeStatusConfigurationSource : IProxyAcmeStatusConfigurationSource
{
    private readonly IProxyConfigurationStore _configurationStore;

    public ProxyAcmeStatusConfigurationSource(IProxyConfigurationStore configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public ProxyAcmeStatusConfigurationSourceReadResult Read()
    {
        if (!_configurationStore.TryGetSnapshot(out var runtimeSnapshot) || runtimeSnapshot is null)
        {
            return ProxyAcmeStatusConfigurationSourceReadResult.MissingConfiguration;
        }

        return ProxyAcmeStatusConfigurationSourceReadResult.Available(new ProxyAcmeStatusConfigurationSourceSnapshot(
            runtimeSnapshot.Acme.Enabled,
            runtimeSnapshot.Acme.DirectoryUrl,
            runtimeSnapshot.Acme.UseStaging,
            runtimeSnapshot.Acme.Certificates
                .Select(static certificate => new ProxyAcmeConfiguredCertificateStatus(
                    certificate.Id,
                    certificate.Enabled,
                    certificate.Domains,
                    certificate.RenewBeforeDays))
                .ToArray(),
            runtimeSnapshot.Certificates
                .Select(static certificate => new ProxyAcmeRuntimeCertificateSource(
                    certificate.Key,
                    certificate.Value.Id,
                    certificate.Value.Source,
                    new DateTimeOffset(certificate.Value.Certificate.NotBefore.ToUniversalTime()),
                    new DateTimeOffset(certificate.Value.Certificate.NotAfter.ToUniversalTime())))
                .ToArray()));
    }
}
