namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeCircuitBreakerProjection
{
    public RuntimeCircuitBreakerProjection(
        bool Enabled,
        int FailureThreshold,
        TimeSpan SamplingWindow,
        TimeSpan OpenDuration,
        int HalfOpenMaxAttempts,
        IReadOnlyList<int> FailureStatusCodes)
    {
        this.Enabled = Enabled;
        this.FailureThreshold = FailureThreshold;
        this.SamplingWindow = SamplingWindow;
        this.OpenDuration = OpenDuration;
        this.HalfOpenMaxAttempts = HalfOpenMaxAttempts;
        this.FailureStatusCodes = RuntimeList.Copy(FailureStatusCodes);
    }

    public bool Enabled { get; init; }

    public int FailureThreshold { get; init; }

    public TimeSpan SamplingWindow { get; init; }

    public TimeSpan OpenDuration { get; init; }

    public int HalfOpenMaxAttempts { get; init; }

    public IReadOnlyList<int> FailureStatusCodes { get; }
}
