using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane;

public static class ProxyRetryPolicy
{
    public static bool IsRetryAllowed(
        RuntimeRoute route,
        Http1RequestHead requestHead,
        out string? skipReason)
    {
        skipReason = null;
        if (!route.Retry.Enabled)
        {
            return false;
        }

        if (!route.Retry.RetryMethods.Any(method => string.Equals(method, requestHead.Method, StringComparison.OrdinalIgnoreCase)))
        {
            skipReason = "method";
            return false;
        }

        if (requestHead.Framing.Kind != Http1BodyKind.None)
        {
            skipReason = "request_body";
            return false;
        }

        return true;
    }

    public static bool ShouldRetry(
        RuntimeRetryPolicy retry,
        ForwardingResult result,
        int attempt,
        int maxAttempts,
        out string? skipReason)
    {
        skipReason = null;
        if (!IsRetryableFailure(retry, result))
        {
            return false;
        }

        if (result.ResponseStarted)
        {
            skipReason = "response_started";
            return false;
        }

        return attempt < maxAttempts;
    }

    public static bool IsRetryableFailure(RuntimeRetryPolicy retry, ForwardingResult result)
    {
        if (result.ResponseStatusCode.HasValue
            && retry.RetryOnStatusCodes.Any(code => code == result.ResponseStatusCode.Value))
        {
            return true;
        }

        if (!result.Succeeded)
        {
            return result.FailureKind switch
            {
                ProxyFailureKind.UpstreamConnectFailed => retry.RetryOnConnectFailure,
                ProxyFailureKind.UpstreamConnectTimeout => retry.RetryOnConnectFailure,
                ProxyFailureKind.UpstreamResponseHeadTimeout => retry.RetryOnUpstreamResponseHeadTimeout,
                _ => false
            };
        }

        return false;
    }
}
