namespace MDRAVA.BLL.ControlPlane;

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

    public ProxyCacheStatusResponse GetStatus()
    {
        return Project(
            _configurationSource.ReadRoutes(),
            _runtimeSource.ReadSnapshot());
    }

    public static ProxyCacheStatusResponse Project(
        IReadOnlyList<ProxyCacheStatusRouteSource> routes,
        ProxyCacheRuntimeStatusSnapshot runtime)
    {
        var routeStatuses = routes
            .Select(route =>
            {
                var routeEntries = runtime.Entries
                    .Where(entry => string.Equals(entry.RouteName, route.RouteName, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                return new ProxyCacheRouteStatus(
                    route.RouteName,
                    route.Enabled,
                    route.MaxEntryBytes,
                    route.MaxTotalBytes,
                    routeEntries.Length,
                    routeEntries.Sum(static entry => entry.SizeBytes));
            })
            .ToArray();

        var rejections = runtime.Rejections
            .OrderBy(static rejection => rejection.Reason, StringComparer.OrdinalIgnoreCase)
            .Select(static rejection => new ProxyCacheRejectionStatus(rejection.Reason, rejection.Count))
            .ToArray();

        return new ProxyCacheStatusResponse(
            runtime.EntryCount,
            runtime.ApproximateBytes,
            runtime.HitCount,
            runtime.MissCount,
            runtime.StoreCount,
            runtime.EvictionCount,
            runtime.StoreRejectionCount,
            runtime.LastClearedAtUtc,
            runtime.LastClearReason,
            rejections,
            routeStatuses);
    }
}
