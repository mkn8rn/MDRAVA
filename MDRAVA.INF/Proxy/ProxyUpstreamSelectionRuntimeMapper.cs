using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.UpstreamSelection;

namespace MDRAVA.INF.Proxy;

internal static class ProxyUpstreamSelectionRuntimeMapper
{
    public static UpstreamSelectionRoute ToSelectionRoute(RuntimeRoute route)
    {
        return new UpstreamSelectionRoute(
            route.Name,
            route.HealthCheck.Enabled,
            route.Upstreams);
    }
}
