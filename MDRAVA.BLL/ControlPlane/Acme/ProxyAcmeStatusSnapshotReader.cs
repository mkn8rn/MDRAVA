namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed class ProxyAcmeStatusSnapshotReader : IProxyAcmeStatusSnapshotReader
{
    private readonly IProxyAcmeStatusConfigurationSource _configurationSource;
    private readonly IProxyAcmeCertificateLifecycleStatusSource _lifecycleStatusSource;

    public ProxyAcmeStatusSnapshotReader(
        IProxyAcmeStatusConfigurationSource configurationSource,
        IProxyAcmeCertificateLifecycleStatusSource lifecycleStatusSource)
    {
        _configurationSource = configurationSource;
        _lifecycleStatusSource = lifecycleStatusSource;
    }

    public bool TryGetSnapshot(out ProxyAcmeStatusSnapshot? snapshot)
    {
        if (!_configurationSource.TryGetSnapshot(out var sourceSnapshot) || sourceSnapshot is null)
        {
            snapshot = null;
            return false;
        }

        var runtimeCertificates = sourceSnapshot.RuntimeCertificates
            .ToDictionary(
                static certificate => certificate.Key,
                static certificate => new ProxyAcmeRuntimeCertificateStatus(
                    certificate.Id,
                    certificate.Source,
                    certificate.NotBeforeUtc,
                    certificate.NotAfterUtc),
                StringComparer.OrdinalIgnoreCase);

        snapshot = new ProxyAcmeStatusSnapshot(
            sourceSnapshot.Enabled,
            sourceSnapshot.DirectoryUrl,
            sourceSnapshot.UseStaging,
            sourceSnapshot.Certificates,
            runtimeCertificates);
        return true;
    }

    public IReadOnlyList<AcmeCertificateLifecycleStatus> GetLifecycleStatuses()
    {
        return _lifecycleStatusSource.GetLifecycleStatuses();
    }
}
