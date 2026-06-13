namespace MDRAVA.BLL.ControlPlane.Caching;

public sealed class ProxyCacheStatusReader : IProxyCacheStatusReader
{
    private readonly IProxyCacheStatusConfigurationSource _configurationSource;
    private readonly IProxyCacheRuntimeStatusSource _runtimeSource;

    public ProxyCacheStatusReader(
        IProxyCacheStatusConfigurationSource configurationSource,
        IProxyCacheRuntimeStatusSource runtimeSource)
    {
        _configurationSource = configurationSource;
        _runtimeSource = runtimeSource;
    }

    public ProxyCacheStatus GetStatus()
    {
        return Project(
            _configurationSource.ReadRoutes(),
            _runtimeSource.ReadSnapshot());
    }

    public static ProxyCacheStatus Project(
        IReadOnlyList<ProxyCacheStatusRouteSource> routes,
        ProxyCacheRuntimeStatusSnapshot runtime)
    {
        var routeStatuses = routes
            .Select(route => ProxyCacheRouteStatus.FromRuntimeEntries(route, runtime.Entries))
            .ToArray();

        var rejections = runtime.Rejections
            .OrderBy(static rejection => rejection.Reason, StringComparer.OrdinalIgnoreCase)
            .Select(ProxyCacheRejectionStatus.FromRuntimeRejection)
            .ToArray();

        return ProxyCacheStatus.FromRuntimeSnapshot(
            runtime,
            rejections,
            routeStatuses);
    }
}
