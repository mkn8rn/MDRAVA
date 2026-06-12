namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed class ProxyAcmeAdministrationService
{
    private readonly IProxyAcmeStatusSnapshotReader _statusReader;

    public ProxyAcmeAdministrationService(IProxyAcmeStatusSnapshotReader statusReader)
    {
        _statusReader = statusReader;
    }

    public AcmeStatusResponse? GetStatus()
    {
        if (!_statusReader.TryGetSnapshot(out var snapshot) || snapshot is null)
        {
            return null;
        }

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

        return AcmeStatusResponse.FromSnapshot(snapshot, statuses);
    }
}
