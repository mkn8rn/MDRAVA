namespace MDRAVA.Tests;

internal static class ProxyGeneratedFailurePolicyTests
{
    public static void AllowsGeneratedFailureOnlyBeforeResponseAndWithoutSuppression()
    {
        AssertEx.True(ProxyGeneratedFailurePolicy.CanWriteFailureResponse(
            responseStarted: false,
            suppressGeneratedFailureResponse: false));
        AssertEx.False(ProxyGeneratedFailurePolicy.CanWriteFailureResponse(
            responseStarted: true,
            suppressGeneratedFailureResponse: false));
        AssertEx.False(ProxyGeneratedFailurePolicy.CanWriteFailureResponse(
            responseStarted: false,
            suppressGeneratedFailureResponse: true));
        AssertEx.False(ProxyGeneratedFailurePolicy.CanWriteFailureResponse(
            responseStarted: true,
            suppressGeneratedFailureResponse: true));
    }

    public static void BuildsGeneratedFailureResponseFromFailureResult()
    {
        var defaultFailure = ForwardingResult.Failure(
            responseStarted: false,
            responseStatusCode: null,
            failureKind: ProxyFailureKind.UpstreamConnectFailed);
        var timeoutFailure = ForwardingResult.Failure(
            responseStarted: false,
            responseStatusCode: 504,
            failureKind: ProxyFailureKind.UpstreamResponseHeadTimeout);

        var defaultResponse = ProxyGeneratedFailurePolicy.BuildFailureResponse((ForwardingResult.FailureResult)defaultFailure);
        var timeoutResponse = ProxyGeneratedFailurePolicy.BuildFailureResponse((ForwardingResult.FailureResult)timeoutFailure);

        AssertEx.Equal(502, defaultResponse.StatusCode);
        AssertEx.Equal("Bad Gateway", defaultResponse.ReasonPhrase);
        AssertEx.Equal(ProxyFailureKind.UpstreamConnectFailed, defaultResponse.FailureKind);
        AssertEx.Equal(504, timeoutResponse.StatusCode);
        AssertEx.Equal("Gateway Timeout", timeoutResponse.ReasonPhrase);
        AssertEx.Equal(ProxyFailureKind.UpstreamResponseHeadTimeout, timeoutResponse.FailureKind);

        var forwardingResult = (ForwardingResult.FailureResult)timeoutResponse.ToForwardingResult();
        AssertEx.True(forwardingResult.ResponseStarted);
        AssertEx.Equal(504, forwardingResult.ResponseStatusCode);
        AssertEx.Equal(ProxyFailureKind.UpstreamResponseHeadTimeout, forwardingResult.FailureKind);
    }

    public static void BuildsGeneratedFailureFramedHeaders()
    {
        var response = new ProxyGeneratedFailureResponse(
            502,
            "Bad Gateway",
            ProxyFailureKind.UpstreamConnectFailed);

        var headers = ProxyGeneratedFailurePolicy.BuildFramedResponseHeaders(
            response,
            "req-789",
            11);

        AssertEx.Equal("text/plain", headers.Single(static header => header.Name == "content-type").Value);
        AssertEx.Equal("req-789", headers.Single(static header => header.Name == "x-request-id").Value);
        AssertEx.Equal("11", headers.Single(static header => header.Name == "content-length").Value);
    }
}
