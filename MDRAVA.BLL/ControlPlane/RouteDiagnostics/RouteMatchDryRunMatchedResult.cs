namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public abstract partial record RouteMatchDryRunResult
{
    public sealed record MatchedRouteResult : RouteMatchDryRunResult
    {
        internal MatchedRouteResult(
            DateTimeOffset evaluatedAtUtc,
            string? noMatchReason,
            RouteMatchDryRunListener listener,
            RouteMatchDryRunRoute route,
            string configuredAction,
            string effectiveAction,
            bool wouldProxy,
            int? generatedStatusCode,
            string originalTarget,
            string rewrittenTarget,
            RouteMatchDryRunUpstream? upstream,
            RouteMatchDryRunPolicy cache,
            RouteMatchDryRunPolicy retry,
            RouteMatchDryRunPolicy circuitBreaker,
            IReadOnlyList<RouteMatchDryRunFinding> findings)
            : base(evaluatedAtUtc, cache, retry, circuitBreaker, findings)
        {
            ArgumentNullException.ThrowIfNull(listener);
            ArgumentNullException.ThrowIfNull(route);
            ArgumentException.ThrowIfNullOrWhiteSpace(configuredAction);
            ArgumentException.ThrowIfNullOrWhiteSpace(effectiveAction);
            ArgumentException.ThrowIfNullOrWhiteSpace(originalTarget);
            ArgumentException.ThrowIfNullOrWhiteSpace(rewrittenTarget);

            NoMatchReason = noMatchReason;
            Listener = listener;
            Route = route;
            ConfiguredAction = configuredAction;
            EffectiveAction = effectiveAction;
            WouldProxy = wouldProxy;
            GeneratedStatusCode = generatedStatusCode;
            OriginalTarget = originalTarget;
            RewrittenTarget = rewrittenTarget;
            Upstream = upstream;
        }

        public string? NoMatchReason { get; }

        public RouteMatchDryRunListener Listener { get; }

        public RouteMatchDryRunRoute Route { get; }

        public string ConfiguredAction { get; }

        public string EffectiveAction { get; }

        public bool WouldProxy { get; }

        public int? GeneratedStatusCode { get; }

        public string OriginalTarget { get; }

        public string RewrittenTarget { get; }

        public RouteMatchDryRunUpstream? Upstream { get; }
    }
}
