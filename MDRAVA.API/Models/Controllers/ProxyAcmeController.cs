using MDRAVA.API.Proxy.Acme;
using MDRAVA.API.Proxy.Configuration.Storage;
using Microsoft.AspNetCore.Mvc;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/acme")]
public sealed class ProxyAcmeController : ControllerBase
{
    private readonly IProxyConfigurationStore _configurationStore;
    private readonly AcmeCertificateStatusStore _statusStore;

    public ProxyAcmeController(
        IProxyConfigurationStore configurationStore,
        AcmeCertificateStatusStore statusStore)
    {
        _configurationStore = configurationStore;
        _statusStore = statusStore;
    }

    [HttpGet("status")]
    public ActionResult<AcmeStatusResponse> Status()
    {
        if (!_configurationStore.TryGetSnapshot(out var snapshot) || snapshot is null)
        {
            return NotFound();
        }

        var statusById = _statusStore.Snapshot()
            .ToDictionary(static status => status.CertificateId, StringComparer.OrdinalIgnoreCase);
        var statuses = snapshot.Acme.Certificates
            .Select(certificate =>
            {
                if (statusById.TryGetValue(certificate.Id, out var status))
                {
                    return status;
                }

                var active = snapshot.Certificates.TryGetValue(certificate.Id, out var runtimeCertificate)
                    && string.Equals(runtimeCertificate.Source, "acme", StringComparison.OrdinalIgnoreCase);
                return new AcmeCertificateLifecycleStatus(
                    certificate.Id,
                    certificate.Enabled,
                    certificate.Domains,
                    active,
                    active ? "acme" : "none",
                    active ? runtimeCertificate!.Certificate.NotBefore.ToUniversalTime() : null,
                    active ? runtimeCertificate!.Certificate.NotAfter.ToUniversalTime() : null,
                    active
                        ? new DateTimeOffset(runtimeCertificate!.Certificate.NotAfter.ToUniversalTime()).AddDays(-certificate.RenewBeforeDays)
                        : null,
                    null,
                    null,
                    null,
                    null,
                    active ? "loaded" : "inactive",
                    null);
            })
            .ToArray();

        return Ok(new AcmeStatusResponse(
            snapshot.Acme.Enabled,
            snapshot.Acme.DirectoryUrl,
            snapshot.Acme.UseStaging,
            statuses));
    }
}
