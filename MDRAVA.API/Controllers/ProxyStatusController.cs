using MDRAVA.API.Proxy.Configuration.Storage;
using MDRAVA.API.Proxy.Hosting;
using MDRAVA.API.Proxy.Metrics;
using Microsoft.AspNetCore.Mvc;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy")]
public sealed class ProxyStatusController : ControllerBase
{
    private readonly ProxyRuntimeState _runtimeState;
    private readonly ProxyMetrics _metrics;
    private readonly IProxyConfigurationStore _configurationStore;

    public ProxyStatusController(
        ProxyRuntimeState runtimeState,
        ProxyMetrics metrics,
        IProxyConfigurationStore configurationStore)
    {
        _runtimeState = runtimeState;
        _metrics = metrics;
        _configurationStore = configurationStore;
    }

    [HttpGet("status")]
    public ProxyStatusResponse Get()
    {
        var runtime = _runtimeState.Snapshot();
        var listenerCount = _configurationStore.TryGetSnapshot(out var snapshot) && snapshot is not null
            ? snapshot.Listeners.Count
            : 0;
        var routeCount = snapshot?.Routes.Count ?? 0;

        return new ProxyStatusResponse(
            runtime.IsRunning,
            runtime.ListenerName,
            runtime.Endpoint,
            runtime.StartedAt,
            runtime.StoppedAt,
            runtime.LastError,
            listenerCount,
            routeCount,
            _metrics.Snapshot());
    }
}
