namespace MDRAVA.BLL.ControlPlane.Caching;

public sealed record ProxyCachePolicyFacts
{
    public ProxyCachePolicyFacts(
        bool Enabled,
        long MaxEntryBytes,
        long MaxTotalBytes,
        TimeSpan DefaultTtl,
        bool RespectOriginCacheControl,
        IReadOnlyList<string> VaryByHeaders,
        IReadOnlyList<int> CacheableStatusCodes,
        IReadOnlyList<string> Methods)
    {
        ArgumentNullException.ThrowIfNull(VaryByHeaders);
        ArgumentNullException.ThrowIfNull(CacheableStatusCodes);
        ArgumentNullException.ThrowIfNull(Methods);

        this.Enabled = Enabled;
        this.MaxEntryBytes = MaxEntryBytes;
        this.MaxTotalBytes = MaxTotalBytes;
        this.DefaultTtl = DefaultTtl;
        this.RespectOriginCacheControl = RespectOriginCacheControl;
        this.VaryByHeaders = CacheList.Copy(VaryByHeaders);
        this.CacheableStatusCodes = CacheList.Copy(CacheableStatusCodes);
        this.Methods = CacheList.Copy(Methods);
    }

    public bool Enabled { get; }

    public long MaxEntryBytes { get; }

    public long MaxTotalBytes { get; }

    public TimeSpan DefaultTtl { get; }

    public bool RespectOriginCacheControl { get; }

    public IReadOnlyList<string> VaryByHeaders { get; }

    public IReadOnlyList<int> CacheableStatusCodes { get; }

    public IReadOnlyList<string> Methods { get; }
}

public sealed record ProxyCacheRequestScope
{
    public ProxyCacheRequestScope(
        string RouteName,
        string RouteHost,
        string Scheme,
        ProxyCachePolicyFacts Policy)
    {
        ArgumentNullException.ThrowIfNull(RouteName);
        ArgumentNullException.ThrowIfNull(RouteHost);
        ArgumentNullException.ThrowIfNull(Scheme);
        ArgumentNullException.ThrowIfNull(Policy);

        this.RouteName = RouteName;
        this.RouteHost = RouteHost;
        this.Scheme = Scheme;
        this.Policy = Policy;
    }

    public string RouteName { get; }

    public string RouteHost { get; }

    public string Scheme { get; }

    public ProxyCachePolicyFacts Policy { get; }
}
