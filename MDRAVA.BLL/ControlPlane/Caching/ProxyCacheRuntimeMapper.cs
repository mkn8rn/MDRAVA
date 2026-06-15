using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Caching;

public static class ProxyCacheRuntimeMapper
{
    public static ProxyCacheRequestScope ToRequestScope(RuntimeRoute route, RuntimeListener listener)
    {
        return new ProxyCacheRequestScope(
            route.Name,
            route.Host,
            RuntimeListenerTransportScheme.FromTransport(listener.Transport),
            ToPolicyFacts(route.Cache));
    }

    public static ProxyCachePolicyFacts ToPolicyFacts(RuntimeCachePolicy policy)
    {
        return new ProxyCachePolicyFacts(
            policy.Enabled,
            policy.MaxEntryBytes,
            policy.MaxTotalBytes,
            policy.DefaultTtl,
            policy.RespectOriginCacheControl,
            policy.VaryByHeaders,
            policy.CacheableStatusCodes,
            policy.Methods);
    }
}
