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

        var currentEntryCount = 0;
        var currentBytes = 0L;
        foreach (var entry in entries)
        {
            if (!string.Equals(entry.RouteName, route.RouteName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            currentEntryCount++;
            currentBytes += entry.SizeBytes;
        }

        return new ProxyCacheRouteStatus(
            route.RouteName,
            route.Enabled,
            route.MaxEntryBytes,
            route.MaxTotalBytes,
            currentEntryCount,
            currentBytes);
    }
}
