namespace MDRAVA.API.Proxy.Configuration;

public static class ProxyOperationalOptionsValidator
{
    private const int MinimumTimeoutMs = 100;
    private const int MaximumTimeoutMs = 10 * 60 * 1000;

    public static IReadOnlyList<string> Validate(ProxyOperationalOptions options)
    {
        List<string> failures = [];
        ValidateTimeout(failures, nameof(options.Timeouts.ClientRequestHeadTimeoutMs), options.Timeouts.ClientRequestHeadTimeoutMs);
        ValidateTimeout(failures, nameof(options.Timeouts.ClientRequestBodyIdleTimeoutMs), options.Timeouts.ClientRequestBodyIdleTimeoutMs);
        ValidateTimeout(failures, nameof(options.Timeouts.UpstreamConnectTimeoutMs), options.Timeouts.UpstreamConnectTimeoutMs);
        ValidateTimeout(failures, nameof(options.Timeouts.UpstreamResponseHeadTimeoutMs), options.Timeouts.UpstreamResponseHeadTimeoutMs);
        ValidateTimeout(failures, nameof(options.Timeouts.UpstreamResponseBodyIdleTimeoutMs), options.Timeouts.UpstreamResponseBodyIdleTimeoutMs);
        ValidateTimeout(failures, nameof(options.Timeouts.DownstreamWriteTimeoutMs), options.Timeouts.DownstreamWriteTimeoutMs);
        return failures;
    }

    private static void ValidateTimeout(List<string> failures, string name, int value)
    {
        if (value is < MinimumTimeoutMs or > MaximumTimeoutMs)
        {
            failures.Add($"Proxy operational timeout {name} must be between {MinimumTimeoutMs} and {MaximumTimeoutMs} milliseconds.");
        }
    }
}
