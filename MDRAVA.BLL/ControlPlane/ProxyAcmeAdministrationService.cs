namespace MDRAVA.BLL.ControlPlane;

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

                var active = snapshot.RuntimeCertificates.TryGetValue(certificate.Id, out var runtimeCertificate)
                    && string.Equals(runtimeCertificate.Source, "acme", StringComparison.OrdinalIgnoreCase);
                return new AcmeCertificateLifecycleStatus(
                    certificate.Id,
                    certificate.Enabled,
                    certificate.Domains,
                    active,
                    active ? "acme" : "none",
                    active ? runtimeCertificate!.NotBeforeUtc : null,
                    active ? runtimeCertificate!.NotAfterUtc : null,
                    active ? runtimeCertificate!.NotAfterUtc.AddDays(-certificate.RenewBeforeDays) : null,
                    null,
                    null,
                    null,
                    null,
                    active ? "loaded" : "inactive",
                    null);
            })
            .ToArray();

        return new AcmeStatusResponse(
            snapshot.Enabled,
            snapshot.DirectoryUrl,
            snapshot.UseStaging,
            statuses);
    }
}
