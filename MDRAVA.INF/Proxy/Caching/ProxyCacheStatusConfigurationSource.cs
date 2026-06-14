using MDRAVA.BLL.ControlPlane.Caching;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.INF.Proxy.Caching;

public sealed class ProxyCacheStatusConfigurationSource
    : IProxyCacheStatusConfigurationSource
{
    private readonly IProxyActiveConfigurationSnapshotReader _configurationStore;

    public ProxyCacheStatusConfigurationSource(IProxyActiveConfigurationSnapshotReader configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public IReadOnlyList<ProxyCacheStatusRouteSource> ReadRoutes()
    {
        return _configurationStore.ReadSnapshot() is ProxyConfigurationSnapshotReadResult.AvailableResult available
            ? ProxyCacheStatusRouteSourceMapper.ToRouteSources(available.Snapshot.Routes)
            : [];
    }
}
