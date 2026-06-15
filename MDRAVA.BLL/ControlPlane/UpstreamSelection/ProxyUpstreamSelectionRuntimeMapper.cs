using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.UpstreamSelection;

public static class ProxyUpstreamSelectionRuntimeMapper
{
    public static UpstreamSelectionRoute ToSelectionRoute(RuntimeRoute route)
    {
        return new UpstreamSelectionRoute(
            route.Name,
            route.HealthCheck.Enabled,
            route.Upstreams);
    }
}
