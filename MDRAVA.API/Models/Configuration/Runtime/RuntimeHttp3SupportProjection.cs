namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeHttp3SupportProjection(
    string RuntimeSupport,
    bool QuicListenerSupported,
    bool QuicConnectionSupported,
    string Configured,
    string EnablementLevel,
    bool EnabledForTraffic,
    bool QuicListenerReady,
    bool AltSvcConfigured,
    bool AltSvcActive,
    int? AltSvcMaxAgeSeconds,
    string DisabledReason,
    bool UdpQuicListenerIdentityModeled,
    string ReadinessConclusion)
{
    public string DefaultEnablementState { get; init; } = "disabled";

    public IReadOnlyList<string> DefaultReadinessBlockers { get; init; } = [];

    public string AltSvcStateReason { get; init; } = "not_configured";

    public string QpackMode { get; init; } = "static_with_zero_dynamic_table";

    public int QpackDynamicTableCapacity { get; init; }

    public int QpackBlockedStreams { get; init; }

    public string RequestBodyMode { get; init; } = "streaming";

    public string ClientHttp3SupportLevel { get; init; } = "default_enabled_for_eligible_tls_proxy_listeners";

    public string UpstreamHttp3SupportLevel { get; init; } = "opt_in_https_quic";

    public IReadOnlyList<string> ClientProtocols { get; init; } = ["http1", "http2", "http3"];

    public IReadOnlyList<string> UpstreamProtocols { get; init; } = ["http1", "http2", "http3"];

    public IReadOnlyList<string> SupportedRouteActions { get; init; } = ["proxy", "redirect", "staticResponse", "maintenance"];

    public IReadOnlyList<string> SupportedPolicyFeatures { get; init; } =
    [
        "cache_get_head",
        "retry_circuit_safe_methods",
        "weighted_balancing",
        "health_checks",
        "path_rewrites",
        "canonical_redirects",
        "http_to_https_redirects",
        "forwarded_headers",
        "request_response_header_policies"
    ];

    public IReadOnlyList<string> UnsupportedFeatures { get; init; } =
    [
        "h3c",
        "connect_over_http3",
        "websocket_over_http3",
        "upstream_http3_multiplexing"
    ];

    public bool UpstreamHttp3Configured { get; init; }

    public string UpstreamPoolingMode { get; init; } = "not_configured";

    public bool UpstreamMultiplexingEnabled { get; init; }

    public int UpstreamMaxStreamsPerConnection { get; init; } = 1;

    public string UpstreamQpackMode { get; init; } = "static_with_zero_dynamic_table";

    public string UpstreamPoolingLimitationReason { get; init; } = "";
}
