namespace MDRAVA.BLL.Configuration;

public static partial class ProxyOperationalOptionsValidationRules
{
    private const int MinimumTimeoutMs = 100;
    private const int MaximumTimeoutMs = 10 * 60 * 1000;
    private const int MinimumDiagnosticsCapacity = 1;
    private const int MaximumDiagnosticsCapacity = 10_000;
    private const int MinimumAuditCapacity = 1;
    private const int MaximumAuditCapacity = 10_000;
    private const long MinimumLogFileBytes = 4 * 1024;
    private const long MaximumLogFileBytes = 1024L * 1024 * 1024;
    private const int MinimumLogFileCount = 1;
    private const int MaximumLogFileCount = 128;

    public static IReadOnlyList<string> Validate(
        ProxyOperationalOptions options,
        Func<string, string?> readEnvironmentVariable,
        IProxyAdminUrlPolicy adminUrlPolicy,
        IProxyRelativeStoragePathPolicy relativeStoragePathPolicy,
        IProxyUrlSyntaxPolicy urlSyntaxPolicy,
        IProxyTrustedProxyPolicy trustedProxyPolicy)
    {
        List<string> failures = [];
        ValidateTimeout(failures, nameof(options.Timeouts.ClientRequestHeadTimeoutMs), options.Timeouts.ClientRequestHeadTimeoutMs);
        ValidateTimeout(failures, nameof(options.Timeouts.ClientRequestBodyIdleTimeoutMs), options.Timeouts.ClientRequestBodyIdleTimeoutMs);
        ValidateTimeout(failures, nameof(options.Timeouts.UpstreamConnectTimeoutMs), options.Timeouts.UpstreamConnectTimeoutMs);
        ValidateTimeout(failures, nameof(options.Timeouts.UpstreamResponseHeadTimeoutMs), options.Timeouts.UpstreamResponseHeadTimeoutMs);
        ValidateTimeout(failures, nameof(options.Timeouts.UpstreamResponseBodyIdleTimeoutMs), options.Timeouts.UpstreamResponseBodyIdleTimeoutMs);
        ValidateTimeout(failures, nameof(options.Timeouts.DownstreamWriteTimeoutMs), options.Timeouts.DownstreamWriteTimeoutMs);
        ValidateTimeout(failures, nameof(options.Timeouts.TlsHandshakeTimeoutMs), options.Timeouts.TlsHandshakeTimeoutMs);
        ValidateTimeout(failures, nameof(options.Timeouts.ClientKeepAliveIdleTimeoutMs), options.Timeouts.ClientKeepAliveIdleTimeoutMs);
        ValidateTimeout(failures, nameof(options.Timeouts.UpstreamIdleConnectionLifetimeMs), options.Timeouts.UpstreamIdleConnectionLifetimeMs);
        ValidateTimeout(failures, nameof(options.Timeouts.TunnelIdleTimeoutMs), options.Timeouts.TunnelIdleTimeoutMs);
        ValidateConnectionLimits(failures, options.Connections);
        ValidateObservability(failures, options.Observability);
        ValidateLimits(failures, options.Limits);
        ValidateForwardedHeaders(failures, options.ForwardedHeaders, trustedProxyPolicy);
        ValidateCertificates(failures, options.Certificates);
        ValidateAdmin(failures, options.Admin, readEnvironmentVariable, adminUrlPolicy);
        ValidateAcme(failures, options.Acme, options.Certificates, relativeStoragePathPolicy, urlSyntaxPolicy);
        ValidateMetrics(failures, options.Metrics);
        return failures;
    }

    private static void ValidateTimeout(List<string> failures, string name, int value)
    {
        if (value is < MinimumTimeoutMs or > MaximumTimeoutMs)
        {
            failures.Add($"Proxy operational timeout {name} must be between {MinimumTimeoutMs} and {MaximumTimeoutMs} milliseconds.");
        }
    }

    private static void ValidateConnectionLimits(List<string> failures, ProxyConnectionOptions options)
    {
        if (options.MaxRequestsPerClientConnection is < 1 or > 100_000)
        {
            failures.Add("Proxy operational connection limit MaxRequestsPerClientConnection must be between 1 and 100000.");
        }

        if (options.MaxIdleUpstreamConnectionsPerUpstream is < 0 or > 10_000)
        {
            failures.Add("Proxy operational connection limit MaxIdleUpstreamConnectionsPerUpstream must be between 0 and 10000.");
        }

        if (options.MaxActiveUpgradedTunnels is < 1 or > 100_000)
        {
            failures.Add("Proxy operational connection limit MaxActiveUpgradedTunnels must be between 1 and 100000.");
        }
    }

    private static void ValidateObservability(List<string> failures, ProxyObservabilityOptions options)
    {
        if (options.RecentDiagnosticsCapacity is < MinimumDiagnosticsCapacity or > MaximumDiagnosticsCapacity)
        {
            failures.Add($"Proxy observability setting RecentDiagnosticsCapacity must be between {MinimumDiagnosticsCapacity} and {MaximumDiagnosticsCapacity}.");
        }

        if (options.LogPersistence.MaxFileBytes is < MinimumLogFileBytes or > MaximumLogFileBytes)
        {
            failures.Add($"Proxy observability log persistence setting MaxFileBytes must be between {MinimumLogFileBytes} and {MaximumLogFileBytes}.");
        }

        if (options.LogPersistence.MaxFiles is < MinimumLogFileCount or > MaximumLogFileCount)
        {
            failures.Add($"Proxy observability log persistence setting MaxFiles must be between {MinimumLogFileCount} and {MaximumLogFileCount}.");
        }
    }

    private static void ValidateLimits(List<string> failures, ProxyLimitsOptions options)
    {
        if (options.MaxActiveClientConnections is < 1 or > 1_000_000)
        {
            failures.Add("Proxy limit MaxActiveClientConnections must be between 1 and 1000000.");
        }

        if (options.MaxConcurrentTlsHandshakes is < 1 or > 100_000)
        {
            failures.Add("Proxy limit MaxConcurrentTlsHandshakes must be between 1 and 100000.");
        }

        if (options.RequestsPerMinutePerIp is < 1 or > 1_000_000)
        {
            failures.Add("Proxy limit RequestsPerMinutePerIp must be between 1 and 1000000.");
        }

        if (options.UpgradeRequestsPerMinutePerIp is < 1 or > 1_000_000)
        {
            failures.Add("Proxy limit UpgradeRequestsPerMinutePerIp must be between 1 and 1000000.");
        }

        if (options.MaxRequestHeadBytes is < 1024 or > 1024 * 1024)
        {
            failures.Add("Proxy limit MaxRequestHeadBytes must be between 1024 and 1048576.");
        }

        if (options.MaxHeaderCount is < 1 or > 10_000)
        {
            failures.Add("Proxy limit MaxHeaderCount must be between 1 and 10000.");
        }

        if (options.MaxHeaderLineBytes is < 64 or > 1024 * 1024)
        {
            failures.Add("Proxy limit MaxHeaderLineBytes must be between 64 and 1048576.");
        }

        if (options.MaxRequestBodyBytes is < 0 or > 1L * 1024 * 1024 * 1024 * 1024)
        {
            failures.Add("Proxy limit MaxRequestBodyBytes must be between 0 and 1099511627776.");
        }

        if (options.MaxPathBytes is < 1 or > 1024 * 1024)
        {
            failures.Add("Proxy limit MaxPathBytes must be between 1 and 1048576.");
        }

        if (options.ShutdownGracePeriodSeconds is < 1 or > 3600)
        {
            failures.Add("Proxy limit ShutdownGracePeriodSeconds must be between 1 and 3600.");
        }
    }

    private static void ValidateMetrics(List<string> failures, ProxyMetricsOptions options)
    {
        if (options.PublicMetricsEnabled)
        {
            failures.Add("Proxy metrics PublicMetricsEnabled is not supported in Phase 17; use the protected /admin/proxy/metrics endpoint.");
        }
    }

    private static bool ContainsControlCharacter(string value)
    {
        return value.Any(char.IsControl);
    }

}
