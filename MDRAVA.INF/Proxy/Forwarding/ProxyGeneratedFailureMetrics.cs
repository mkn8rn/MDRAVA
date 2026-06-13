using MDRAVA.BLL.ControlPlane.Forwarding;
using MDRAVA.BLL.ControlPlane.Metrics;

namespace MDRAVA.INF.Proxy.Forwarding;

internal static class ProxyGeneratedFailureMetrics
{
    public static void Record(ProxyMetrics metrics, ProxyFailureKind failureKind)
    {
        var response = ProxyGeneratedFailurePolicy.BuildFailureResponse(failureKind);
        Record(metrics, response);
    }

    public static void Record(ProxyMetrics metrics, ProxyGeneratedFailureResponse response)
    {
        metrics.GeneratedFailureResponse(response.StatusCode);
    }
}
