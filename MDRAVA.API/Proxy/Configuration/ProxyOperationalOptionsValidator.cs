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
        ValidateTimeout(failures, nameof(options.Timeouts.TlsHandshakeTimeoutMs), options.Timeouts.TlsHandshakeTimeoutMs);
        ValidateTimeout(failures, nameof(options.Timeouts.ClientKeepAliveIdleTimeoutMs), options.Timeouts.ClientKeepAliveIdleTimeoutMs);
        ValidateTimeout(failures, nameof(options.Timeouts.UpstreamIdleConnectionLifetimeMs), options.Timeouts.UpstreamIdleConnectionLifetimeMs);
        ValidateTimeout(failures, nameof(options.Timeouts.TunnelIdleTimeoutMs), options.Timeouts.TunnelIdleTimeoutMs);
        ValidateConnectionLimits(failures, options.Connections);
        ValidateCertificates(failures, options.Certificates);
        return failures;
    }

    private static void ValidateTimeout(List<string> failures, string name, int value)
    {
        if (value is < MinimumTimeoutMs or > MaximumTimeoutMs)
        {
            failures.Add($"Proxy operational timeout {name} must be between {MinimumTimeoutMs} and {MaximumTimeoutMs} milliseconds.");
        }
    }

    private static void ValidateCertificates(List<string> failures, IReadOnlyList<CertificateOptions> certificates)
    {
        HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < certificates.Count; index++)
        {
            var certificate = certificates[index];
            var prefix = $"Proxy:Certificates:{index}";

            if (string.IsNullOrWhiteSpace(certificate.Id))
            {
                failures.Add($"{prefix}:Id is required.");
            }
            else if (!ids.Add(certificate.Id))
            {
                failures.Add($"{prefix}:Id '{certificate.Id}' is duplicated.");
            }

            if (!string.Equals(certificate.Format, "pfx", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"{prefix}:Format must be 'pfx' for Phase 5.");
            }

            if (string.IsNullOrWhiteSpace(certificate.Path))
            {
                failures.Add($"{prefix}:Path is required.");
            }

            if (!string.IsNullOrEmpty(certificate.Password)
                && !string.IsNullOrWhiteSpace(certificate.PasswordEnvironmentVariable))
            {
                failures.Add($"{prefix} must not set both Password and PasswordEnvironmentVariable.");
            }
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
}
