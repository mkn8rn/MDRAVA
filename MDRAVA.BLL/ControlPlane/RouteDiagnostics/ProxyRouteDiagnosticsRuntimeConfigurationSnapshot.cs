using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

internal sealed class ProxyRouteDiagnosticsRuntimeConfigurationSnapshot
    : IProxyRouteDiagnosticsConfigurationSnapshot
{
    public ProxyRouteDiagnosticsRuntimeConfigurationSnapshot(
        IReadOnlyList<RuntimeListener> runtimeListeners,
        IReadOnlyList<RuntimeRoute> runtimeRoutes)
    {
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
