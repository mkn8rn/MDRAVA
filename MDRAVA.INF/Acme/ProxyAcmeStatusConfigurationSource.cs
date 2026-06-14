using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.INF.Acme;

public sealed class ProxyAcmeStatusConfigurationSource : IProxyAcmeStatusConfigurationSource
{
    private readonly IProxyConfigurationStore _configurationStore;

    public ProxyAcmeStatusConfigurationSource(IProxyConfigurationStore configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public ProxyAcmeStatusConfigurationSourceReadResult Read()
    {
        var snapshotResult = _configurationStore.ReadSnapshot();
        if (snapshotResult is not ProxyConfigurationSnapshotReadResult.AvailableResult available)
        {
            return ProxyAcmeStatusConfigurationSourceReadResult.MissingConfiguration;
        }

        var runtimeSnapshot = available.Snapshot;
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
