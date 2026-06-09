namespace MDRAVA.BLL.ControlPlane;

public static class ProxyForwardingFailurePolicy
{
    public static bool IsCircuitFailure(ProxyFailureKind failureKind)
    {
        return failureKind is ProxyFailureKind.UpstreamConnectFailed
            or ProxyFailureKind.UpstreamConnectTimeout
            or ProxyFailureKind.UpstreamResponseHeadTimeout;
    }

    public static string CircuitFailureReason(ProxyFailureKind failureKind)
    {
        return failureKind switch
        {
            ProxyFailureKind.UpstreamConnectFailed => "connect_failure",
            ProxyFailureKind.UpstreamConnectTimeout => "connect_timeout",
            ProxyFailureKind.UpstreamResponseHeadTimeout => "response_head_timeout",
            _ => "other"
        };
    }

    public static int StatusCodeForFailure(ProxyFailureKind failureKind)
    {
        return failureKind switch
        {
            ProxyFailureKind.UpstreamConnectTimeout => 504,
            ProxyFailureKind.UpstreamResponseHeadTimeout => 504,
            ProxyFailureKind.RequestPayloadTooLarge => 413,
            ProxyFailureKind.ClientMalformedRequest => 400,
            _ => 502
        };
    }
}
