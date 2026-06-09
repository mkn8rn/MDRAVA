
namespace MDRAVA.API.Proxy.Acme;

public sealed class ProxyAcmeStatusConfigurationSource : IProxyAcmeStatusConfigurationSource
{
    private readonly IProxyConfigurationStore _configurationStore;

    public ProxyAcmeStatusConfigurationSource(IProxyConfigurationStore configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public bool TryGetSnapshot(out ProxyAcmeStatusConfigurationSourceSnapshot? snapshot)
    {
        if (!_configurationStore.TryGetSnapshot(out var runtimeSnapshot) || runtimeSnapshot is null)
        {
            snapshot = null;
            return false;
        }

        snapshot = new ProxyAcmeStatusConfigurationSourceSnapshot(
            runtimeSnapshot.Acme.Enabled,
            runtimeSnapshot.Acme.DirectoryUrl,
            runtimeSnapshot.Acme.UseStaging,
            runtimeSnapshot.Acme.Certificates
                .Select(static certificate => new ProxyAcmeConfiguredCertificateSource(
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
                .ToArray());
        return true;
    }
}
