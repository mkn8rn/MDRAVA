namespace MDRAVA.BLL.ControlPlane.Caching;

public interface IProxyCacheStatusConfigurationSource
{
    IReadOnlyList<ProxyCacheStatusRouteSource> ReadRoutes();
}

public interface IProxyCacheRuntimeStatusSource
{
    ProxyCacheRuntimeStatusSnapshot ReadSnapshot();
}

public sealed record ProxyCacheStatusRouteSource
{
    public ProxyCacheStatusRouteSource(
        string RouteName,
        bool Enabled,
        long MaxEntryBytes,
        long MaxTotalBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(RouteName);
        ArgumentOutOfRangeException.ThrowIfNegative(MaxEntryBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(MaxTotalBytes);

        this.RouteName = RouteName;
        this.Enabled = Enabled;
        this.MaxEntryBytes = MaxEntryBytes;
        this.MaxTotalBytes = MaxTotalBytes;
    }

    public string RouteName { get; }

    public bool Enabled { get; }

    public long MaxEntryBytes { get; }

    public long MaxTotalBytes { get; }
}

public sealed record ProxyCacheRuntimeStatusSnapshot
{
    public ProxyCacheRuntimeStatusSnapshot(
        int EntryCount,
        long ApproximateBytes,
        long HitCount,
        long MissCount,
        long StoreCount,
        long EvictionCount,
        long StoreRejectionCount,
        DateTimeOffset? LastClearedAtUtc,
        string? LastClearReason,
        IReadOnlyList<ProxyCacheRuntimeRejectionSnapshot> Rejections,
        IReadOnlyList<ProxyCacheRuntimeEntrySnapshot> Entries)
    {
        ArgumentNullException.ThrowIfNull(Rejections);
        ArgumentNullException.ThrowIfNull(Entries);
        ArgumentOutOfRangeException.ThrowIfNegative(EntryCount);
        ArgumentOutOfRangeException.ThrowIfNegative(ApproximateBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(HitCount);
        ArgumentOutOfRangeException.ThrowIfNegative(MissCount);
        ArgumentOutOfRangeException.ThrowIfNegative(StoreCount);
        ArgumentOutOfRangeException.ThrowIfNegative(EvictionCount);
        ArgumentOutOfRangeException.ThrowIfNegative(StoreRejectionCount);

        this.EntryCount = EntryCount;
        this.ApproximateBytes = ApproximateBytes;
        this.HitCount = HitCount;
        this.MissCount = MissCount;
        this.StoreCount = StoreCount;
        this.EvictionCount = EvictionCount;
        this.StoreRejectionCount = StoreRejectionCount;
        this.LastClearedAtUtc = LastClearedAtUtc;
        this.LastClearReason = LastClearReason;
        this.Rejections = CacheList.Copy(Rejections);
        this.Entries = CacheList.Copy(Entries);
    }

    public int EntryCount { get; }

    public long ApproximateBytes { get; }

    public long HitCount { get; }

    public long MissCount { get; }

    public long StoreCount { get; }

    public long EvictionCount { get; }

    public long StoreRejectionCount { get; }

    public DateTimeOffset? LastClearedAtUtc { get; }

    public string? LastClearReason { get; }

    public IReadOnlyList<ProxyCacheRuntimeRejectionSnapshot> Rejections { get; }

    public IReadOnlyList<ProxyCacheRuntimeEntrySnapshot> Entries { get; }
}

public sealed record ProxyCacheRuntimeRejectionSnapshot
{
    public ProxyCacheRuntimeRejectionSnapshot(
        string Reason,
        long Count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Reason);
        ArgumentOutOfRangeException.ThrowIfNegative(Count);

        this.Reason = Reason;
        this.Count = Count;
    }

    public string Reason { get; }

    public long Count { get; }
}

public sealed record ProxyCacheRuntimeEntrySnapshot
{
    public ProxyCacheRuntimeEntrySnapshot(
        string RouteName,
        long SizeBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(RouteName);
        ArgumentOutOfRangeException.ThrowIfNegative(SizeBytes);

        this.RouteName = RouteName;
        this.SizeBytes = SizeBytes;
    }

    public string RouteName { get; }

    public long SizeBytes { get; }
}
