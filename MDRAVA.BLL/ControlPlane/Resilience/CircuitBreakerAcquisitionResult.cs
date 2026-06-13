namespace MDRAVA.BLL.ControlPlane.Resilience;

public abstract record CircuitBreakerAcquisitionResult
{
    private CircuitBreakerAcquisitionResult()
    {
    }

    public static CircuitBreakerAcquisitionResult Rejected { get; } = new RejectedResult();

    public static CircuitBreakerAcquisitionResult Accepted(CircuitBreakerLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        return new AcceptedResult(lease);
    }

    public sealed record AcceptedResult : CircuitBreakerAcquisitionResult
    {
        public AcceptedResult(CircuitBreakerLease lease)
        {
            ArgumentNullException.ThrowIfNull(lease);
            Lease = lease;
        }

        public CircuitBreakerLease Lease { get; }
    }

    public sealed record RejectedResult : CircuitBreakerAcquisitionResult;
}
