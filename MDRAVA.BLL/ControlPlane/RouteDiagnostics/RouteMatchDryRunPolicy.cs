namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed record RouteMatchDryRunPolicy(
    bool Enabled,
    bool WouldApply,
    string Reason)
{
    public static RouteMatchDryRunPolicy Disabled(string reason)
    {
        return new RouteMatchDryRunPolicy(
            Enabled: false,
            WouldApply: false,
            reason);
    }

    public static RouteMatchDryRunPolicy EnabledButBlocked(string reason)
    {
        return new RouteMatchDryRunPolicy(
            Enabled: true,
            WouldApply: false,
            reason);
    }

    public static RouteMatchDryRunPolicy EnabledAndApplies(string reason)
    {
        return new RouteMatchDryRunPolicy(
            Enabled: true,
            WouldApply: true,
            reason);
    }
}
