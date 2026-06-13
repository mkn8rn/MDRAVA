namespace MDRAVA.BLL.ControlPlane.Forwarding;

public abstract record ForwardingResult
{
    private ForwardingResult(
        bool responseStarted,
        bool keepClientConnectionOpen,
        int? responseStatusCode,
        ProxyFailureKind failureKind)
    {
        ResponseStarted = responseStarted;
        KeepClientConnectionOpen = keepClientConnectionOpen;
        ResponseStatusCode = responseStatusCode;
        FailureKind = failureKind;
    }

    public bool ResponseStarted { get; }

    public bool KeepClientConnectionOpen { get; }

    public int? ResponseStatusCode { get; }

    public ProxyFailureKind FailureKind { get; }

    public static ForwardingResult Success(
        bool responseStarted,
        bool keepClientConnectionOpen,
        int? responseStatusCode = null)
    {
        return new SuccessResult(
            responseStarted,
            keepClientConnectionOpen,
            responseStatusCode);
    }

    public static ForwardingResult TunnelCompleted(
        int responseStatusCode,
        TunnelRelayResult tunnel)
    {
        return new TunnelCompletedResult(responseStatusCode, tunnel);
    }

    public static ForwardingResult Failure(
        bool responseStarted,
        int? responseStatusCode,
        ProxyFailureKind failureKind)
    {
        return new FailureResult(
            responseStarted,
            responseStatusCode,
            failureKind);
    }

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
