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

        Listeners = RouteDiagnosticsList.Copy(runtimeListeners
            .Select(static listener => new ProxyRouteDiagnosticsRuntimeListener(listener)));
        Routes = RouteDiagnosticsList.Copy(runtimeRoutes
            .Select(static route => new ProxyRouteDiagnosticsRuntimeRoute(route)));
    }

    public IReadOnlyList<IProxyRouteDiagnosticsListener> Listeners { get; }

    public IReadOnlyList<IProxyRouteDiagnosticsRoute> Routes { get; }
}

public static class ProxyRouteDiagnosticsRuntimeConfigurationSnapshotMapper
{
    public static ProxyRouteDiagnosticsRuntimeConfigurationSnapshot FromConfiguration(
        ProxyConfigurationSnapshot snapshot)
    {
        return new ProxyRouteDiagnosticsRuntimeConfigurationSnapshot(
            snapshot.Listeners,
            snapshot.Routes);
    }
}
