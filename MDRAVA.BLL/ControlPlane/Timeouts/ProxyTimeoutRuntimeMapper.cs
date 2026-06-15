using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Timeouts;

public static class ProxyTimeoutRuntimeMapper
{
    public static ProxyRouteTimeoutPolicyInput ToPolicyInput(RuntimeRoute route)
    {
        return new ProxyRouteTimeoutPolicyInput(
            route.ResolvedOptions.UpstreamResponseHeadTimeout,
            route.Retry.PerAttemptTimeout);
    }
}
