namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public abstract record ProxyRouteDiagnosticsRequestDecision
{
    private ProxyRouteDiagnosticsRequestDecision()
    {
    }

    public static ProxyRouteDiagnosticsRequestDecision Accepted(ProxyRouteDiagnosticsRequestInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new AcceptedDecision(input);
    }

    public static ProxyRouteDiagnosticsRequestDecision Rejected(RouteMatchDryRunResult.FailedResult failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new RejectedDecision(failure);
    }

    public sealed record AcceptedDecision(ProxyRouteDiagnosticsRequestInput Input) : ProxyRouteDiagnosticsRequestDecision;

    public sealed record RejectedDecision(RouteMatchDryRunResult.FailedResult Failure) : ProxyRouteDiagnosticsRequestDecision;
}
