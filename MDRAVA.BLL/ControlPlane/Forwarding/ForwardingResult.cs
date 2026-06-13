namespace MDRAVA.BLL.ControlPlane.Forwarding;

public abstract partial record ForwardingResult
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

}
