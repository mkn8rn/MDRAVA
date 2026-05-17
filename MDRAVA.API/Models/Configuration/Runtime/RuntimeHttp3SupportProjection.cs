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

    public bool UpstreamHttp3Configured { get; init; }

    public string UpstreamPoolingMode { get; init; } = "not_configured";

    public bool UpstreamMultiplexingEnabled { get; init; }

    public int UpstreamMaxStreamsPerConnection { get; init; } = 1;

    public string UpstreamQpackMode { get; init; } = "static_with_zero_dynamic_table";

    public string UpstreamPoolingLimitationReason { get; init; } = "";
}
