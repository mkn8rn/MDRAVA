using MDRAVA.BLL.ControlPlane.Routing;
using MDRAVA.BLL.Http;

namespace MDRAVA.BLL.ControlPlane.Forwarding;

public sealed record ProxyGeneratedFailureResponse(
    int StatusCode,
    string ReasonPhrase,
    string Body,
    ProxyFailureKind FailureKind)
{
    public ProxyGeneratedFailureResponse(
        int statusCode,
        string reasonPhrase,
        ProxyFailureKind failureKind)
        : this(statusCode, reasonPhrase, reasonPhrase, failureKind)
    {
    }

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
    private const string ContentType = "text/plain";

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

    public static ProxyGeneratedFailureResponse BuildFailureResponse(
        int statusCode,
        string reasonPhrase,
        string body,
        ProxyFailureKind failureKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonPhrase);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        return new ProxyGeneratedFailureResponse(
            statusCode,
            reasonPhrase,
            body,
            failureKind);
    }

    public static IReadOnlyList<ProxyHeaderField> BuildFramedResponseHeaders(
        ProxyGeneratedFailureResponse response,
        string requestId,
        int bodyByteLength)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentOutOfRangeException.ThrowIfNegative(bodyByteLength);

        return
        [
            new ProxyHeaderField("content-type", ContentType),
            new ProxyHeaderField("x-request-id", requestId),
            new ProxyHeaderField("content-length", bodyByteLength.ToString(System.Globalization.CultureInfo.InvariantCulture))
        ];
    }
}
