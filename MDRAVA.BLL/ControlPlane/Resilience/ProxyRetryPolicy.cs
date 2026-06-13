using MDRAVA.BLL.ControlPlane.Http1;
using MDRAVA.BLL.ControlPlane.Forwarding;
using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Resilience;

public static class ProxyRetryPolicy
{
    public static ProxyRetryPlan CreatePlan(RuntimeRoute route, Http1RequestHead requestHead)
    {
        var admission = EvaluateAdmission(route, requestHead);
        var isAllowed = admission == ProxyRetryAdmissionDecision.Allowed;
        return new ProxyRetryPlan(
            admission,
            isAllowed,
            isAllowed ? route.Retry.MaxAttempts : 1);
    }

    public static ProxyRetryAdmissionDecision EvaluateAdmission(RuntimeRoute route, Http1RequestHead requestHead)
    {
        if (!route.Retry.Enabled)
        {
            return ProxyRetryAdmissionDecision.NotAllowed;
        }

        if (!route.Retry.RetryMethods.Any(method => string.Equals(method, requestHead.Method, StringComparison.OrdinalIgnoreCase)))
        {
            return ProxyRetryAdmissionDecision.Skipped("method");
        }

        if (requestHead.Framing.Kind != Http1BodyKind.None)
        {
            return ProxyRetryAdmissionDecision.Skipped("request_body");
        }

        return ProxyRetryAdmissionDecision.Allowed;
    }

    public static ProxyRetryAttemptDecision EvaluateAttempt(
        RuntimeRetryPolicy retry,
        ForwardingResult result,
        int attempt,
        int maxAttempts)
    {
        if (!IsRetryableFailure(retry, result))
        {
            return ProxyRetryAttemptDecision.Stop;
        }

        if (result.ResponseStarted)
        {
            return ProxyRetryAttemptDecision.Skipped("response_started");
        }

        return attempt < maxAttempts
            ? ProxyRetryAttemptDecision.Retry
            : ProxyRetryAttemptDecision.Stop;
    }

    public static bool ShouldSuppressRetryableStatusResponse(
        RuntimeRetryPolicy retry,
        int statusCode,
        bool suppressRetryableStatusResponse)
    {
        return suppressRetryableStatusResponse
            && retry.Enabled
            && retry.RetryOnStatusCodes.Any(code => code == statusCode);
    }

    public static bool ShouldSuppressAttemptFailureResponse(
        bool retryAllowed,
        int attempt,
        int maxAttempts)
    {
        return retryAllowed
            && attempt < maxAttempts;
    }

    public static bool DidExhaustAttempts(
        RuntimeRetryPolicy retry,
        ForwardingResult result,
        int attempt,
        int maxAttempts)
    {
        return attempt == maxAttempts
            && IsRetryableFailure(retry, result);
    }

    public static bool DidExhaustAttemptsBeforeUpstreamSelection(int attempt)
    {
        return attempt > 1;
    }

    public static bool IsRetryableFailure(RuntimeRetryPolicy retry, ForwardingResult result)
    {
        if (result.ResponseStatusCode.HasValue
            && retry.RetryOnStatusCodes.Any(code => code == result.ResponseStatusCode.Value))
        {
            return true;
        }

        if (result is ForwardingResult.FailureResult)
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

public sealed record ProxyRetryPlan(
    ProxyRetryAdmissionDecision Admission,
    bool IsAllowed,
    int MaxAttempts);

public abstract record ProxyRetryAdmissionDecision
{
    private ProxyRetryAdmissionDecision()
    {
    }

    public static ProxyRetryAdmissionDecision Allowed { get; } = new AllowedDecision();

    public static ProxyRetryAdmissionDecision NotAllowed { get; } = new NotAllowedDecision();

    public static ProxyRetryAdmissionDecision Skipped(string reason)
    {
        return new SkippedDecision(reason);
    }

    public sealed record SkippedDecision : ProxyRetryAdmissionDecision
    {
        public SkippedDecision(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Retry admission skip reason is required.", nameof(reason));
            }

            Reason = reason;
        }

        public string Reason { get; }
    }

    private sealed record AllowedDecision : ProxyRetryAdmissionDecision;

    private sealed record NotAllowedDecision : ProxyRetryAdmissionDecision;
}

public abstract record ProxyRetryAttemptDecision
{
    private ProxyRetryAttemptDecision()
    {
    }

    public static ProxyRetryAttemptDecision Retry { get; } = new RetryDecision();

    public static ProxyRetryAttemptDecision Stop { get; } = new StopDecision();

    public static ProxyRetryAttemptDecision Skipped(string reason)
    {
        return new SkippedDecision(reason);
    }

    public sealed record SkippedDecision : ProxyRetryAttemptDecision
    {
        public SkippedDecision(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Retry attempt skip reason is required.", nameof(reason));
            }

            Reason = reason;
        }

        public string Reason { get; }
    }

    private sealed record RetryDecision : ProxyRetryAttemptDecision;

    private sealed record StopDecision : ProxyRetryAttemptDecision;
}
