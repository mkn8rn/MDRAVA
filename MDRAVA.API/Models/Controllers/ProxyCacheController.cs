using MDRAVA.API.Proxy.Caching;
using MDRAVA.API.Proxy.Configuration.Storage;
using Microsoft.AspNetCore.Mvc;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/cache")]
public sealed class ProxyCacheController : ControllerBase
{
    private readonly ResponseCacheStore _cacheStore;
    private readonly IProxyConfigurationStore _configurationStore;

    public ProxyCacheController(
        ResponseCacheStore cacheStore,
        IProxyConfigurationStore configurationStore)
    {
        _cacheStore = cacheStore;
        _configurationStore = configurationStore;
    }

    [HttpGet("status")]
    public ProxyCacheStatusResponse Status()
    {
        _configurationStore.TryGetSnapshot(out var snapshot);
        return _cacheStore.Snapshot(snapshot);
    }

    [HttpPost("clear")]
    public ProxyCacheStatusResponse Clear()
    {
        _cacheStore.Clear("manual");
        _configurationStore.TryGetSnapshot(out var snapshot);
        return _cacheStore.Snapshot(snapshot);
    }
}
