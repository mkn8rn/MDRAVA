namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed class ProxyAcmeAdministrationService
{
    private readonly IProxyAcmeStatusSnapshotReader _statusReader;

    public ProxyAcmeAdministrationService(IProxyAcmeStatusSnapshotReader statusReader)
    {
        _statusReader = statusReader;
    }

    public AcmeStatus? GetStatus()
    {
        var snapshotResult = _statusReader.ReadSnapshot();
        if (snapshotResult is not ProxyAcmeStatusSnapshotReadResult.AvailableResult available)
        {
            return null;
        }

        var snapshot = available.Snapshot;
        var statusById = _statusReader.GetLifecycleStatuses()
            .ToDictionary(static status => status.CertificateId, StringComparer.OrdinalIgnoreCase);
        var statuses = snapshot.Certificates
            .Select(certificate =>
            {
                if (statusById.TryGetValue(certificate.Id, out var status))
                {
                    return status;
                }

                snapshot.RuntimeCertificates.TryGetValue(certificate.Id, out var runtimeCertificate);
                return AcmeCertificateLifecycleStatus.FromConfiguredCertificate(
                    certificate,
                    runtimeCertificate);
            })
            .ToArray();

        return AcmeStatus.FromSnapshot(snapshot, statuses);
    }
}
