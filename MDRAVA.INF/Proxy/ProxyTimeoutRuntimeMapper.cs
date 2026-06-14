using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Timeouts;

namespace MDRAVA.INF.Proxy;

internal static class ProxyTimeoutRuntimeMapper
{
    public static ProxyRouteTimeoutPolicyInput ToPolicyInput(RuntimeRoute route)
    {
        return new ProxyRouteTimeoutPolicyInput(
            route.ResolvedOptions.UpstreamResponseHeadTimeout,
            route.Retry.PerAttemptTimeout);
    }
}
