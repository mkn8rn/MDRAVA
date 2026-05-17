namespace MDRAVA.API.Models.Diagnostics;

public sealed record RouteMatchDryRunResult(
    bool Succeeded,
    DateTimeOffset EvaluatedAtUtc,
    string? FailureReason,
    string? NoMatchReason,
    RouteMatchDryRunListener? Listener,
    RouteMatchDryRunRoute? Route,
    string? ConfiguredAction,
    string? EffectiveAction,
    bool WouldProxy,
    int? GeneratedStatusCode,
    string? OriginalTarget,
    string? RewrittenTarget,
    RouteMatchDryRunUpstream? Upstream,
    RouteMatchDryRunPolicy Cache,
    RouteMatchDryRunPolicy Retry,
    RouteMatchDryRunPolicy CircuitBreaker,
    IReadOnlyList<RouteMatchDryRunFinding> Findings);
