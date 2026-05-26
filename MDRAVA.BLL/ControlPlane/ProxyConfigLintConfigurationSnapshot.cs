namespace MDRAVA.BLL.ControlPlane;

public sealed record ProxyConfigLintConfigurationSnapshot(
    IReadOnlyList<string> SourceFiles,
    ProxyConfigLintAdminSecurity AdminSecurity,
    ProxyConfigLintMetricsOptions Metrics,
    bool Http3QuicConnectionSupported,
    IReadOnlyList<ProxyConfigLintListener> Listeners,
    IReadOnlyList<ProxyConfigLintRoute> Routes);

public sealed record ProxyConfigLintAdminSecurity(
    IReadOnlyList<string> Urls,
    bool RequireAuthentication);

public sealed record ProxyConfigLintMetricsOptions(
    bool PublicMetricsEnabled);

public sealed record ProxyConfigLintListener(
    string Name,
    string Address,
    int Port,
    bool Enabled,
    string Transport,
    bool Http3Configured,
    bool Http3EnabledForTraffic,
    string Http3DisabledReason,
    string Http3EnablementLevel,
    bool Http3AltSvcEnabled,
    string? QuicIdentityKey);

public sealed record ProxyConfigLintRoute(
    string Name,
    string SiteName,
    string Host,
    string PathPrefix,
    string Action,
    bool HttpsRedirectEnabled,
    bool CanonicalHostEnabled,
    string CanonicalHostTargetHost,
    bool CacheEnabled,
    IReadOnlyList<string> CacheVaryByHeaders,
    bool RetryEnabled,
    IReadOnlyList<string> RetryMethods,
    bool HealthCheckEnabled,
    IReadOnlyList<ProxyConfigLintUpstream> Upstreams,
    string StaticResponseBody);

public sealed record ProxyConfigLintUpstream(
    string Name,
    string Scheme,
    string Protocol,
    bool TlsValidateCertificate,
    bool CircuitBreakerEnabled);
