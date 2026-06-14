using MDRAVA.BLL.ControlPlane.ConfigurationManagement;
using MDRAVA.BLL.ControlPlane.HealthChecks;

namespace MDRAVA.INF.Proxy.Health;

public sealed class ProxyConfigurationUpstreamHealthCheckTargetSource : IUpstreamHealthCheckTargetSource
{
    private readonly IProxyActiveConfigurationSnapshotReader _configurationStore;

    public ProxyConfigurationUpstreamHealthCheckTargetSource(IProxyActiveConfigurationSnapshotReader configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public IReadOnlyList<UpstreamHealthCheckTarget> ReadTargets()
    {
        return _configurationStore.ReadSnapshot() is ProxyConfigurationSnapshotReadResult.AvailableResult available
            ? UpstreamHealthCheckTargetMapper.FromRoutes(available.Snapshot.Routes)
            : [];
    }
}
