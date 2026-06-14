using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Http1;
using MDRAVA.BLL.ControlPlane.Resilience;

namespace MDRAVA.INF.Proxy;

internal static class ProxyRetryRuntimeMapper
{
    public static ProxyRetryAdmissionInput ToAdmissionInput(
        RuntimeRoute route,
        Http1RequestHead requestHead)
    {
        return new ProxyRetryAdmissionInput(
            route.Retry.Enabled,
            route.Retry.MaxAttempts,
            route.Retry.RetryMethods,
            requestHead.Method,
            requestHead.Framing.Kind != Http1BodyKind.None);
    }

    public static ProxyRetryOutcomeInput ToOutcomeInput(RuntimeRetryPolicy retry)
    {
        return new ProxyRetryOutcomeInput(
            retry.Enabled,
            retry.RetryOnConnectFailure,
            retry.RetryOnUpstreamResponseHeadTimeout,
            retry.RetryOnStatusCodes);
    }
}
