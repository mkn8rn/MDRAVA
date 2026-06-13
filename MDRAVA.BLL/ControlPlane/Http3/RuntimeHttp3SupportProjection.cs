namespace MDRAVA.BLL.ControlPlane.Http3;

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
    private IReadOnlyList<string> _defaultReadinessBlockers = Http3List.Copy<string>([]);
    private IReadOnlyList<string> _clientProtocols = Http3List.Copy(["http1", "http2", "http3"]);
    private IReadOnlyList<string> _upstreamProtocols = Http3List.Copy(["http1", "http2", "http3"]);
    private IReadOnlyList<string> _supportedRouteActions = Http3List.Copy(["proxy", "redirect", "staticResponse", "maintenance"]);
    private IReadOnlyList<string> _supportedPolicyFeatures = Http3List.Copy(
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
    ]);
    private IReadOnlyList<string> _unsupportedFeatures = Http3List.Copy(RuntimeHttp3UnsupportedFeatureCodes.EffectiveConfig);

    public string DefaultEnablementState { get; init; } = "disabled";

    public IReadOnlyList<string> DefaultReadinessBlockers
    {
        get => _defaultReadinessBlockers;
        init => _defaultReadinessBlockers = Http3List.Copy(value);
    }

    public string AltSvcStateReason { get; init; } = "not_configured";

    public string QpackMode { get; init; } = "static_with_zero_dynamic_table";

    public int QpackDynamicTableCapacity { get; init; }

    public int QpackBlockedStreams { get; init; }

    public string RequestBodyMode { get; init; } = "streaming";

    public string ClientHttp3SupportLevel { get; init; } = "default_enabled_for_eligible_tls_proxy_listeners";

    public string UpstreamHttp3SupportLevel { get; init; } = "opt_in_https_quic";

    public IReadOnlyList<string> ClientProtocols
    {
        get => _clientProtocols;
        init => _clientProtocols = Http3List.Copy(value);
    }

    public IReadOnlyList<string> UpstreamProtocols
    {
        get => _upstreamProtocols;
        init => _upstreamProtocols = Http3List.Copy(value);
    }

    public IReadOnlyList<string> SupportedRouteActions
    {
        get => _supportedRouteActions;
        init => _supportedRouteActions = Http3List.Copy(value);
    }

    public IReadOnlyList<string> SupportedPolicyFeatures
    {
        get => _supportedPolicyFeatures;
        init => _supportedPolicyFeatures = Http3List.Copy(value);
    }

    public IReadOnlyList<string> UnsupportedFeatures
    {
        get => _unsupportedFeatures;
        init => _unsupportedFeatures = Http3List.Copy(value);
    }

    public bool UpstreamHttp3Configured { get; init; }

    public string UpstreamPoolingMode { get; init; } = "not_configured";

    public bool UpstreamMultiplexingEnabled { get; init; }

    public int UpstreamMaxStreamsPerConnection { get; init; } = 8;

    public string UpstreamQpackMode { get; init; } = "static_with_zero_dynamic_table";

    public string UpstreamPoolingLimitationReason { get; init; } = "";
}
