using Microsoft.AspNetCore.Mvc;
using BusinessProxyMetricsAdministrationService = MDRAVA.BLL.ControlPlane.Metrics.ProxyMetricsAdministrationService;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/metrics")]
public sealed class ProxyMetricsController : ControllerBase
{
    private readonly BusinessProxyMetricsAdministrationService _metricsAdministration;

    public ProxyMetricsController(BusinessProxyMetricsAdministrationService metricsAdministration)
    {
        _metricsAdministration = metricsAdministration;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return ProxyAdminHttpResultMapper.TextExportOrNotFound(this, _metricsAdministration.Export());
    }
}
