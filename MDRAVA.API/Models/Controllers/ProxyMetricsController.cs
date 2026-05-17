using System.Text;
using MDRAVA.API.Proxy.Configuration.Storage;
using MDRAVA.API.Proxy.Metrics;
using Microsoft.AspNetCore.Mvc;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/metrics")]
public sealed class ProxyMetricsController : ControllerBase
{
    private readonly IProxyConfigurationStore _configurationStore;
    private readonly PrometheusMetricsExporter _exporter;

    public ProxyMetricsController(
        IProxyConfigurationStore configurationStore,
        PrometheusMetricsExporter exporter)
    {
        _configurationStore = configurationStore;
        _exporter = exporter;
    }

    [HttpGet]
    public IActionResult Get()
    {
        if (!_configurationStore.TryGetSnapshot(out var snapshot) || snapshot is null)
        {
            return NotFound();
        }

        if (!snapshot.Metrics.Enabled)
        {
            return NotFound();
        }

        return Content(
            _exporter.Export(snapshot),
            PrometheusMetricsExporter.ContentType,
            Encoding.UTF8);
    }
}
