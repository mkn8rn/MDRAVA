namespace MDRAVA.API.Models.Configuration;

public sealed class ProxyRetryPolicyOptions
{
    public bool Enabled { get; init; }

    public int MaxAttempts { get; init; } = 1;

    public int? PerAttemptTimeoutMs { get; init; }

    public bool RetryOnConnectFailure { get; init; }

    public bool RetryOnUpstreamResponseHeadTimeout { get; init; }

    public List<int> RetryOnStatusCodes { get; init; } = [];

    public List<string> RetryMethods { get; init; } = ["GET", "HEAD"];

    public int RetryBackoffMilliseconds { get; init; }
}
