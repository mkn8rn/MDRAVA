namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed partial class RouteMatchDiagnosticsService
{
    private static string? MetricReason(RouteMatchDryRunResult result)
    {
        return result switch
        {
            RouteMatchDryRunResult.FailedResult failed => failed.FailureReason,
            RouteMatchDryRunResult.NoMatchingListenerResult noListener => noListener.NoMatchReason,
            RouteMatchDryRunResult.NoMatchingRouteResult noRoute => noRoute.NoMatchReason,
            RouteMatchDryRunResult.MatchedRouteResult matched => matched.NoMatchReason,
            _ => throw new InvalidOperationException($"Unknown route dry-run result '{result.GetType().Name}'.")
        };
    }
}
