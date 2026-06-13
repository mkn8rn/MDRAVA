namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public abstract partial record RouteMatchDryRunResult
{
    public sealed record FailedResult : RouteMatchDryRunResult
    {
        internal FailedResult(
            DateTimeOffset evaluatedAtUtc,
            string failureReason,
            RouteMatchDryRunPolicy cache,
            RouteMatchDryRunPolicy retry,
            RouteMatchDryRunPolicy circuitBreaker,
            IReadOnlyList<RouteMatchDryRunFinding> findings)
            : base(evaluatedAtUtc, cache, retry, circuitBreaker, findings)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(failureReason);

            FailureReason = failureReason;
        }

        public string FailureReason { get; }
    }

    public sealed record NoMatchingListenerResult : RouteMatchDryRunResult
    {
        internal NoMatchingListenerResult(
            DateTimeOffset evaluatedAtUtc,
            string noMatchReason,
            string originalTarget,
            RouteMatchDryRunPolicy cache,
            RouteMatchDryRunPolicy retry,
            RouteMatchDryRunPolicy circuitBreaker,
            IReadOnlyList<RouteMatchDryRunFinding> findings)
            : base(evaluatedAtUtc, cache, retry, circuitBreaker, findings)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(noMatchReason);
            ArgumentException.ThrowIfNullOrWhiteSpace(originalTarget);

            NoMatchReason = noMatchReason;
            OriginalTarget = originalTarget;
        }

        public string NoMatchReason { get; }

        public string OriginalTarget { get; }
    }

    public sealed record NoMatchingRouteResult : RouteMatchDryRunResult
    {
        internal NoMatchingRouteResult(
            DateTimeOffset evaluatedAtUtc,
            string noMatchReason,
            RouteMatchDryRunListener listener,
            string originalTarget,
            RouteMatchDryRunPolicy cache,
            RouteMatchDryRunPolicy retry,
            RouteMatchDryRunPolicy circuitBreaker,
            IReadOnlyList<RouteMatchDryRunFinding> findings)
            : base(evaluatedAtUtc, cache, retry, circuitBreaker, findings)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(noMatchReason);
            ArgumentNullException.ThrowIfNull(listener);
            ArgumentException.ThrowIfNullOrWhiteSpace(originalTarget);

            NoMatchReason = noMatchReason;
            Listener = listener;
            OriginalTarget = originalTarget;
        }

        public string NoMatchReason { get; }

        public RouteMatchDryRunListener Listener { get; }

        public string OriginalTarget { get; }
    }
}
