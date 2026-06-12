namespace MDRAVA.BLL.ControlPlane.Caching;

public sealed record ProxyCachePolicyFacts(
    bool Enabled,
    long MaxEntryBytes,
    long MaxTotalBytes,
    TimeSpan DefaultTtl,
    bool RespectOriginCacheControl,
    IReadOnlyList<string> VaryByHeaders,
    IReadOnlyList<int> CacheableStatusCodes,
    IReadOnlyList<string> Methods);

public sealed record ProxyCacheRequestScope(
    string RouteName,
    string RouteHost,
    string Scheme,
    ProxyCachePolicyFacts Policy);
