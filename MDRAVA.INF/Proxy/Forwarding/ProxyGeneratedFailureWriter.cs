using MDRAVA.BLL.ControlPlane.Forwarding;
using MDRAVA.BLL.ControlPlane.Metrics;
using MDRAVA.BLL.Configuration;

namespace MDRAVA.INF.Proxy.Forwarding;

internal static class ProxyGeneratedFailureWriter
{
    public static ValueTask WriteAsync(
        Stream clientStream,
        ProxyFailureKind failureKind,
        RuntimeTimeouts timeouts,
        string requestId,
        ProxyMetrics metrics,
        CancellationToken cancellationToken)
    {
        var response = ProxyGeneratedFailurePolicy.BuildFailureResponse(failureKind);
        ProxyGeneratedFailureMetrics.Record(metrics, response);

        return ProxyErrorResponses.WriteGeneratedFailureAsync(
            clientStream,
            response,
            requestId,
            timeouts.DownstreamWriteTimeout,
            metrics,
            cancellationToken);
    }
}
