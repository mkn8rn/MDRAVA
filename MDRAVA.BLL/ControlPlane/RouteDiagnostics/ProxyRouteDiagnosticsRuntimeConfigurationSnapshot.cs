using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed class ProxyRouteDiagnosticsRuntimeConfigurationSnapshot
    : IProxyRouteDiagnosticsConfigurationSnapshot
{
    public ProxyRouteDiagnosticsRuntimeConfigurationSnapshot(
        IEnumerable<RuntimeListener> runtimeListeners,
        IEnumerable<RuntimeRoute> runtimeRoutes)
    {
        ArgumentNullException.ThrowIfNull(runtimeListeners);
        ArgumentNullException.ThrowIfNull(runtimeRoutes);

        Listeners = RouteDiagnosticsList.Copy(runtimeListeners.Select(ToListener));
        Routes = RouteDiagnosticsList.Copy(runtimeRoutes.Select(ToRoute));
    }

    public IReadOnlyList<IProxyRouteDiagnosticsListener> Listeners { get; }

    public IReadOnlyList<IProxyRouteDiagnosticsRoute> Routes { get; }

    private static ProxyRouteDiagnosticsRuntimeListener ToListener(RuntimeListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);

        return new ProxyRouteDiagnosticsRuntimeListener(listener);
    }

    private static ProxyRouteDiagnosticsRuntimeRoute ToRoute(RuntimeRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);

        return new ProxyRouteDiagnosticsRuntimeRoute(route);
    }
}

public static class ProxyRouteDiagnosticsRuntimeConfigurationSnapshotMapper
{
    public static ProxyRouteDiagnosticsRuntimeConfigurationSnapshot FromSources(
        IEnumerable<RuntimeListener> runtimeListeners,
        IEnumerable<RuntimeRoute> runtimeRoutes)
    {
        return new ProxyRouteDiagnosticsRuntimeConfigurationSnapshot(
            runtimeListeners,
            runtimeRoutes);
    }
}
