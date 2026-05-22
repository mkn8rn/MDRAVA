using MDRAVA.API.Proxy.Configuration.Storage;

namespace MDRAVA.API.Proxy.Acme;

public sealed class ProxyAcmeStatusSnapshotReader : IProxyAcmeStatusSnapshotReader
{
    private readonly IProxyConfigurationStore _configurationStore;
    private readonly AcmeCertificateStatusStore _statusStore;

    public ProxyAcmeStatusSnapshotReader(
        IProxyConfigurationStore configurationStore,
        AcmeCertificateStatusStore statusStore)
    {
        _configurationStore = configurationStore;
        _statusStore = statusStore;
    }

    public bool TryGetSnapshot(out ProxyAcmeStatusSnapshot? snapshot)
    {
        if (!_configurationStore.TryGetSnapshot(out var runtimeSnapshot) || runtimeSnapshot is null)
        {
            snapshot = null;
            return false;
        }

        var certificates = runtimeSnapshot.Acme.Certificates
            .Select(static certificate => new ProxyAcmeConfiguredCertificateStatus(
                certificate.Id,
                certificate.Enabled,
                certificate.Domains,
                certificate.RenewBeforeDays))
            .ToArray();
        var runtimeCertificates = runtimeSnapshot.Certificates
            .ToDictionary(
                static certificate => certificate.Key,
                static certificate => new ProxyAcmeRuntimeCertificateStatus(
                    certificate.Value.Id,
                    certificate.Value.Source,
                    new DateTimeOffset(certificate.Value.Certificate.NotBefore.ToUniversalTime()),
                    new DateTimeOffset(certificate.Value.Certificate.NotAfter.ToUniversalTime())),
                StringComparer.OrdinalIgnoreCase);

        snapshot = new ProxyAcmeStatusSnapshot(
            runtimeSnapshot.Acme.Enabled,
            runtimeSnapshot.Acme.DirectoryUrl,
            runtimeSnapshot.Acme.UseStaging,
            certificates,
            runtimeCertificates);
        return true;
    }

    public IReadOnlyList<AcmeCertificateLifecycleStatus> GetLifecycleStatuses()
    {
        return _statusStore.Snapshot();
    }
}
