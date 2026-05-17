using MDRAVA.API.Proxy.Configuration.Storage;
using MDRAVA.API.Proxy.Health;
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
    private readonly UpstreamHealthStore _healthStore;

    public ProxyStatusController(
        ProxyRuntimeState runtimeState,
        ProxyMetrics metrics,
        IProxyConfigurationStore configurationStore,
        UpstreamHealthStore healthStore)
    {
        _runtimeState = runtimeState;
        _metrics = metrics;
        _configurationStore = configurationStore;
        _healthStore = healthStore;
    }

    [HttpGet("status")]
    public ProxyStatusResponse Get()
    {
        var runtime = _runtimeState.Snapshot();
        var listenerCount = _configurationStore.TryGetSnapshot(out var snapshot) && snapshot is not null
            ? snapshot.Listeners.Count
            : 0;
        var routeCount = snapshot?.Routes.Count ?? 0;

        var upstreams = _healthStore.Snapshot(snapshot)
            .Select(static upstream => new ProxyUpstreamStatusResponse(
                upstream.RouteName,
                upstream.UpstreamName,
                upstream.Endpoint,
                upstream.Scheme,
                upstream.TlsCertificateValidationEnabled,
                upstream.SniHost,
                upstream.HealthCheckEnabled,
                upstream.State,
                upstream.LastResult,
                upstream.LastCheckedAtUtc,
                upstream.ConsecutiveSuccesses,
                upstream.ConsecutiveFailures,
                upstream.SelectedRequests,
                upstream.RequestFailures)
            {
                Protocol = upstream.Protocol,
                Weight = upstream.Weight,
                CircuitBreaker = upstream.CircuitBreaker
            })
            .ToArray();

        return new ProxyStatusResponse(
            runtime.IsRunning,
            runtime.ListenerName,
            runtime.Endpoint,
            runtime.StartedAt,
            runtime.StoppedAt,
            runtime.LastError,
            runtime.IsShuttingDown,
            runtime.ShutdownStartedAtUtc,
            runtime.ShutdownDeadlineUtc,
            snapshot?.Version,
            snapshot?.LoadedAtUtc,
            listenerCount,
            routeCount,
            _metrics.Snapshot(),
            upstreams)
        {
            Listeners = runtime.Listeners,
            LastListenerReload = runtime.LastListenerReload
        };
    }
}
