namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeRetryProjection
{
    public RuntimeRetryProjection(
        bool Enabled,
        int MaxAttempts,
        TimeSpan? PerAttemptTimeout,
        bool RetryOnConnectFailure,
        bool RetryOnUpstreamResponseHeadTimeout,
        IEnumerable<int> RetryOnStatusCodes,
        IEnumerable<string> RetryMethods,
        TimeSpan RetryBackoff)
    {
        this.Enabled = Enabled;
        this.MaxAttempts = MaxAttempts;
        this.PerAttemptTimeout = PerAttemptTimeout;
        this.RetryOnConnectFailure = RetryOnConnectFailure;
        this.RetryOnUpstreamResponseHeadTimeout = RetryOnUpstreamResponseHeadTimeout;
        this.RetryOnStatusCodes = RuntimeList.Copy(RetryOnStatusCodes);
        this.RetryMethods = RuntimeList.Copy(RetryMethods);
        this.RetryBackoff = RetryBackoff;
    }

    public bool Enabled { get; init; }

    public int MaxAttempts { get; init; }

    public TimeSpan? PerAttemptTimeout { get; init; }

    public bool RetryOnConnectFailure { get; init; }

    public bool RetryOnUpstreamResponseHeadTimeout { get; init; }

    public IReadOnlyList<int> RetryOnStatusCodes { get; }

    public IReadOnlyList<string> RetryMethods { get; }

    public TimeSpan RetryBackoff { get; init; }
}
