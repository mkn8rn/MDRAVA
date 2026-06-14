using MDRAVA.BLL.ControlPlane.Forwarding;
using System.Collections.ObjectModel;

namespace MDRAVA.BLL.ControlPlane.Resilience;

public static class ProxyRetryPolicy
{
    public static ProxyRetryPlan CreatePlan(ProxyRetryAdmissionInput input)
    {
        var admission = EvaluateAdmission(input);
        var isAllowed = admission == ProxyRetryAdmissionDecision.Allowed;
        return new ProxyRetryPlan(
            admission,
            isAllowed,
            isAllowed ? Math.Max(1, input.MaxAttempts) : 1);
    }

    public static ProxyRetryAdmissionDecision EvaluateAdmission(ProxyRetryAdmissionInput input)
    {
        if (!input.Enabled)
        {
            return ProxyRetryAdmissionDecision.NotAllowed;
        }

        if (!input.RetryMethods.Any(method => string.Equals(method, input.RequestMethod, StringComparison.OrdinalIgnoreCase)))
        {
            return ProxyRetryAdmissionDecision.Skipped("method");
        }

        if (input.HasRequestBody)
        {
            return ProxyRetryAdmissionDecision.Skipped("request_body");
        }

        return ProxyRetryAdmissionDecision.Allowed;
    }

    public static ProxyRetryAttemptDecision EvaluateAttempt(
        ProxyRetryOutcomeInput input,
        ForwardingResult result,
        int attempt,
        int maxAttempts)
    {
        if (!IsRetryableFailure(input, result))
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
        ProxyRetryOutcomeInput input,
        int statusCode,
        bool suppressRetryableStatusResponse)
    {
        return suppressRetryableStatusResponse
            && input.Enabled
            && input.RetryOnStatusCodes.Any(code => code == statusCode);
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
        ProxyRetryOutcomeInput input,
        ForwardingResult result,
        int attempt,
        int maxAttempts)
    {
        return attempt == maxAttempts
            && IsRetryableFailure(input, result);
    }

    public static bool DidExhaustAttemptsBeforeUpstreamSelection(int attempt)
    {
        return attempt > 1;
    }

    public static ForwardingResult RequireCompletedAttemptResult(ForwardingResult? result)
    {
        return result ?? throw new InvalidOperationException("Retry attempt loop completed without running an attempt.");
    }

    public static bool IsRetryableFailure(ProxyRetryOutcomeInput input, ForwardingResult result)
    {
        if (result.ResponseStatusCode.HasValue
            && input.RetryOnStatusCodes.Any(code => code == result.ResponseStatusCode.Value))
        {
            return true;
        }

        if (result is ForwardingResult.FailureResult)
        {
            return result.FailureKind switch
            {
                ProxyFailureKind.UpstreamConnectFailed => input.RetryOnConnectFailure,
                ProxyFailureKind.UpstreamConnectTimeout => input.RetryOnConnectFailure,
                ProxyFailureKind.UpstreamResponseHeadTimeout => input.RetryOnUpstreamResponseHeadTimeout,
                _ => false
            };
        }

        return false;
    }
}

public sealed record ProxyRetryOutcomeInput
{
    public ProxyRetryOutcomeInput(
        bool Enabled,
        bool RetryOnConnectFailure,
        bool RetryOnUpstreamResponseHeadTimeout,
        IReadOnlyList<int> RetryOnStatusCodes)
    {
        ArgumentNullException.ThrowIfNull(RetryOnStatusCodes);

        this.Enabled = Enabled;
        this.RetryOnConnectFailure = RetryOnConnectFailure;
        this.RetryOnUpstreamResponseHeadTimeout = RetryOnUpstreamResponseHeadTimeout;
        this.RetryOnStatusCodes = new ReadOnlyCollection<int>(RetryOnStatusCodes.ToArray());
    }

    public bool Enabled { get; }

    public bool RetryOnConnectFailure { get; }

    public bool RetryOnUpstreamResponseHeadTimeout { get; }

    public IReadOnlyList<int> RetryOnStatusCodes { get; }
}

public sealed record ProxyRetryAdmissionInput
{
    public ProxyRetryAdmissionInput(
        bool Enabled,
        int MaxAttempts,
        IReadOnlyList<string> RetryMethods,
        string RequestMethod,
        bool HasRequestBody)
    {
        ArgumentNullException.ThrowIfNull(RetryMethods);

        this.Enabled = Enabled;
        this.MaxAttempts = MaxAttempts;
        this.RetryMethods = new ReadOnlyCollection<string>(RetryMethods.ToArray());
        this.RequestMethod = RequestMethod;
        this.HasRequestBody = HasRequestBody;
    }

    public bool Enabled { get; }

    public int MaxAttempts { get; }

    public IReadOnlyList<string> RetryMethods { get; }

    public string RequestMethod { get; }

    public bool HasRequestBody { get; }
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
