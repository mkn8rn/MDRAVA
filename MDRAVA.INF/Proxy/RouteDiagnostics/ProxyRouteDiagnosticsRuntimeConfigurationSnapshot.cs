using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.RouteDiagnostics;

namespace MDRAVA.INF.Proxy.RouteDiagnostics;

public sealed class ProxyRouteDiagnosticsRuntimeConfigurationSnapshot
    : IProxyRouteDiagnosticsConfigurationSnapshot
{
    public ProxyRouteDiagnosticsRuntimeConfigurationSnapshot(
        IReadOnlyList<RuntimeListener> runtimeListeners,
        IReadOnlyList<RuntimeRoute> runtimeRoutes)
    {
        ArgumentNullException.ThrowIfNull(runtimeListeners);
        ArgumentNullException.ThrowIfNull(runtimeRoutes);

        Listeners = runtimeListeners
            .Select(static listener => new ProxyRouteDiagnosticsRuntimeListener(listener))
            .ToArray();
        Routes = runtimeRoutes
            .Select(static route => new ProxyRouteDiagnosticsRuntimeRoute(route))
            .ToArray();
    }

    public IReadOnlyList<IProxyRouteDiagnosticsListener> Listeners { get; }

    public IReadOnlyList<IProxyRouteDiagnosticsRoute> Routes { get; }
}
