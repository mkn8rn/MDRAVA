using MDRAVA.BLL.ControlPlane.Routing;

namespace MDRAVA.BLL.ControlPlane.Forwarding;

public sealed record ProxyGeneratedFailureResponse(
    int StatusCode,
    string ReasonPhrase,
    ProxyFailureKind FailureKind)
{
    public ForwardingResult ToForwardingResult()
    {
        return ForwardingResult.Failure(
            responseStarted: true,
            responseStatusCode: StatusCode,
            failureKind: FailureKind);
    }
}

public static class ProxyGeneratedFailurePolicy
{
    public static bool CanWriteFailureResponse(
        bool responseStarted,
        bool suppressGeneratedFailureResponse)
    {
        return !responseStarted && !suppressGeneratedFailureResponse;
    }

    public static ProxyGeneratedFailureResponse BuildFailureResponse(ForwardingResult.FailureResult failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        var statusCode = failure.ResponseStatusCode ?? ProxyForwardingFailurePolicy.StatusCodeForFailure(failure.FailureKind);
        return new ProxyGeneratedFailureResponse(
            statusCode,
            ProxyRouteActionPolicy.ReasonPhrase(statusCode),
            failure.FailureKind);
    }
}
