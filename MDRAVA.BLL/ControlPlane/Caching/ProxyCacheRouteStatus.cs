namespace MDRAVA.BLL.ControlPlane.Caching;

public sealed record ProxyCacheRouteStatus
{
    private ProxyCacheRouteStatus(
        string routeName,
        bool enabled,
        long maxEntryBytes,
        long maxTotalBytes,
        int currentEntryCount,
        long currentBytes)
    {
        RouteName = routeName;
        Enabled = enabled;
        MaxEntryBytes = maxEntryBytes;
        MaxTotalBytes = maxTotalBytes;
        CurrentEntryCount = currentEntryCount;
        CurrentBytes = currentBytes;
    }

    public string RouteName { get; }

    public bool Enabled { get; }

    public long MaxEntryBytes { get; }

    public long MaxTotalBytes { get; }

    public int CurrentEntryCount { get; }

    public long CurrentBytes { get; }

    public static ProxyCacheRouteStatus FromRuntimeEntries(
        ProxyCacheStatusRouteSource route,
        IReadOnlyList<ProxyCacheRuntimeEntrySnapshot> entries)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(entries);

        var routeEntries = entries
            .Where(entry => string.Equals(entry.RouteName, route.RouteName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return new ProxyCacheRouteStatus(
            route.RouteName,
            route.Enabled,
            route.MaxEntryBytes,
            route.MaxTotalBytes,
            routeEntries.Length,
            routeEntries.Sum(static entry => entry.SizeBytes));
    }
}
