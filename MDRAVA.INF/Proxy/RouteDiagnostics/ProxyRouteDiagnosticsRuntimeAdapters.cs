using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;
using MDRAVA.BLL.ControlPlane.RouteDiagnostics;

namespace MDRAVA.INF.Proxy.RouteDiagnostics;

public sealed class ProxyRouteDiagnosticsConfigurationSource
    : IProxyRouteDiagnosticsConfigurationSource
{
    private readonly IProxyActiveConfigurationSnapshotReader _configurationStore;

    public ProxyRouteDiagnosticsConfigurationSource(IProxyActiveConfigurationSnapshotReader configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public ProxyRouteDiagnosticsConfigurationReadResult Read()
    {
        var snapshotResult = _configurationStore.ReadSnapshot();
        if (snapshotResult is not ProxyConfigurationSnapshotReadResult.AvailableResult available)
        {
            return ProxyRouteDiagnosticsConfigurationReadResult.MissingConfiguration;
        }

        var runtimeSnapshot = available.Snapshot;
        return ProxyRouteDiagnosticsConfigurationReadResult.Available(
            new ProxyRouteDiagnosticsRuntimeConfigurationSnapshot(runtimeSnapshot.Listeners, runtimeSnapshot.Routes));
    }
}
