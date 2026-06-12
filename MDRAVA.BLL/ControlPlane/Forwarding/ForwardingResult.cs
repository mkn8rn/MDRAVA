namespace MDRAVA.BLL.ControlPlane.Forwarding;

public sealed record ForwardingResult
{
    private ForwardingResult(
        bool succeeded,
        bool responseStarted,
        bool keepClientConnectionOpen,
        int? responseStatusCode,
        ProxyFailureKind failureKind,
        TunnelRelayResult? tunnel)
    {
        Succeeded = succeeded;
        ResponseStarted = responseStarted;
        KeepClientConnectionOpen = keepClientConnectionOpen;
        ResponseStatusCode = responseStatusCode;
        FailureKind = failureKind;
        Tunnel = tunnel;
    }

    public bool Succeeded { get; }

    public bool ResponseStarted { get; }

    public bool KeepClientConnectionOpen { get; }

    public int? ResponseStatusCode { get; }

    public ProxyFailureKind FailureKind { get; }

    public TunnelRelayResult? Tunnel { get; }

    public static ForwardingResult Success(
        bool responseStarted,
        bool keepClientConnectionOpen,
        int? responseStatusCode = null)
    {
        return new ForwardingResult(
            succeeded: true,
            responseStarted: responseStarted,
            keepClientConnectionOpen: keepClientConnectionOpen,
            responseStatusCode: responseStatusCode,
            failureKind: ProxyFailureKind.None,
            tunnel: null);
    }

    public static ForwardingResult TunnelCompleted(
        int responseStatusCode,
        TunnelRelayResult tunnel)
    {
        return new ForwardingResult(
            succeeded: true,
            responseStarted: true,
            keepClientConnectionOpen: false,
            responseStatusCode: responseStatusCode,
            failureKind: tunnel.FailureKind,
            tunnel: tunnel);
    }

    public static ForwardingResult Failure(
        bool responseStarted,
        int? responseStatusCode,
        ProxyFailureKind failureKind)
    {
        return new ForwardingResult(
            succeeded: false,
            responseStarted: responseStarted,
            keepClientConnectionOpen: false,
            responseStatusCode: responseStatusCode,
            failureKind: failureKind,
            tunnel: null);
    }
}
