namespace MDRAVA.BLL.Configuration;

public static partial class ProxyOperationalOptionsValidationRules
{
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
}
