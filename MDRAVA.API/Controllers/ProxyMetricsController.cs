using MDRAVA.BLL.ControlPlane;
using Microsoft.AspNetCore.Mvc;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/metrics")]
public sealed class ProxyMetricsController : ControllerBase
{
    private readonly ProxyMetricsAdministrationService _metricsAdministration;

    public ProxyMetricsController(ProxyMetricsAdministrationService metricsAdministration)
    {
        _metricsAdministration = metricsAdministration;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return ProxyAdminHttpResultMapper.TextExportOrNotFound(this, _metricsAdministration.Export());
    }
}
