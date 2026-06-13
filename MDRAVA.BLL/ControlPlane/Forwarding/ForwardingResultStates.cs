namespace MDRAVA.BLL.ControlPlane.Forwarding;

public abstract partial record ForwardingResult
{
    public sealed record SuccessResult : ForwardingResult
    {
        internal SuccessResult(
            bool responseStarted,
            bool keepClientConnectionOpen,
            int? responseStatusCode)
            : base(
                responseStarted,
                keepClientConnectionOpen,
                responseStatusCode,
                ProxyFailureKind.None)
        {
        }
    }

    public sealed record TunnelCompletedResult : ForwardingResult
    {
        internal TunnelCompletedResult(
            int responseStatusCode,
            TunnelRelayResult tunnel)
            : base(
                responseStarted: true,
                keepClientConnectionOpen: false,
                responseStatusCode,
                tunnel?.FailureKind ?? throw new ArgumentNullException(nameof(tunnel)))
        {
            Tunnel = tunnel;
        }

        public TunnelRelayResult Tunnel { get; }
    }

    public sealed record FailureResult : ForwardingResult
    {
        internal FailureResult(
            bool responseStarted,
            int? responseStatusCode,
            ProxyFailureKind failureKind)
            : base(
                responseStarted,
                keepClientConnectionOpen: false,
                responseStatusCode,
                failureKind)
        {
            if (failureKind == ProxyFailureKind.None)
            {
                throw new ArgumentException("Forwarding failures must name a concrete failure kind.", nameof(failureKind));
            }
        }
    }
}
