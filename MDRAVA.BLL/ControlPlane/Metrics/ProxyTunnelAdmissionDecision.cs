namespace MDRAVA.BLL.ControlPlane.Metrics;

public abstract record ProxyTunnelAdmissionDecision
{
    private ProxyTunnelAdmissionDecision()
    {
    }

    public static ProxyTunnelAdmissionDecision Accepted { get; } = new AcceptedResult();

    public static ProxyTunnelAdmissionDecision Rejected { get; } = new RejectedResult();

    public sealed record AcceptedResult : ProxyTunnelAdmissionDecision;

    public sealed record RejectedResult : ProxyTunnelAdmissionDecision;
}
