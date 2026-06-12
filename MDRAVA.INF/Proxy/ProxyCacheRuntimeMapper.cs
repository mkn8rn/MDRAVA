using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Caching;

namespace MDRAVA.INF.Proxy;

internal static class ProxyCacheRuntimeMapper
{
    public static ProxyCacheRequestScope ToRequestScope(RuntimeRoute route, RuntimeListener listener)
    {
        return new ProxyCacheRequestScope(
            route.Name,
            route.Host,
            listener.Transport == RuntimeListenerTransport.Https ? "https" : "http",
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
