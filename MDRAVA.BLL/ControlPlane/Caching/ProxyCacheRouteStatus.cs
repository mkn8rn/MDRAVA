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

    public static ProxyCacheRouteStatus FromSources(
        string routeName,
        bool enabled,
        long maxEntryBytes,
        long maxTotalBytes,
        int currentEntryCount,
        long currentBytes)
    {
        return new ProxyCacheRouteStatus(
            routeName,
            enabled,
            maxEntryBytes,
            maxTotalBytes,
            currentEntryCount,
            currentBytes);
    }
}
