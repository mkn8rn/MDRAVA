using System.Text;
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
        var result = _metricsAdministration.Export();
        if (!result.Available)
        {
            return NotFound();
        }

        return Content(
            result.Content,
            result.ContentType,
            Encoding.UTF8);
    }
}
