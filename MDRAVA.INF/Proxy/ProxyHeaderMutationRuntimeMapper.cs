using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Headers;

namespace MDRAVA.INF.Proxy;

internal static class ProxyHeaderMutationRuntimeMapper
{
    public static ProxyHeaderMutationPolicyInput ToPolicyInput(RuntimeHeaderPolicy policy)
    {
        return new ProxyHeaderMutationPolicyInput(
            policy.SetRequestHeaders,
            policy.RemoveRequestHeaders,
            policy.SetResponseHeaders,
            policy.RemoveResponseHeaders);
    }
}
