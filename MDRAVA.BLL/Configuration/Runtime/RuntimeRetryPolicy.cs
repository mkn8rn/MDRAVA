namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeRetryPolicy(
    bool Enabled,
    int MaxAttempts,
    TimeSpan? PerAttemptTimeout,
    bool RetryOnConnectFailure,
    bool RetryOnUpstreamResponseHeadTimeout,
    IReadOnlyList<int> RetryOnStatusCodes,
    IReadOnlyList<string> RetryMethods,
    TimeSpan RetryBackoff)
{
    public static RuntimeRetryPolicy Disabled { get; } = new(
        false,
        1,
        null,
        false,
        false,
        [],
        ["GET", "HEAD"],
        TimeSpan.Zero);
}
