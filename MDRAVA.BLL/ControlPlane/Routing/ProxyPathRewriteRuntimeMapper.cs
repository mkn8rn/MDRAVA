using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Routing;

public static class ProxyPathRewriteRuntimeMapper
{
    public static PathRewritePolicyInput ToPolicyInput(RuntimeRoute route)
    {
        return new PathRewritePolicyInput(
            route.PathRewrite.StripPrefix,
            route.PathRewrite.ReplacePrefix,
            route.PathRewrite.Replacement);
    }
}
