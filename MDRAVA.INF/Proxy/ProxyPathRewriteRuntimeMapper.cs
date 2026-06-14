using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Routing;

namespace MDRAVA.INF.Proxy;

internal static class ProxyPathRewriteRuntimeMapper
{
    public static PathRewritePolicyInput ToPolicyInput(RuntimeRoute route)
    {
        return new PathRewritePolicyInput(
            route.PathRewrite.StripPrefix,
            route.PathRewrite.ReplacePrefix,
            route.PathRewrite.Replacement);
    }
}
