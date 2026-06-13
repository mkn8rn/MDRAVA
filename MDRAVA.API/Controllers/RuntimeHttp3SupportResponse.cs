using MDRAVA.BLL.ControlPlane.Http3;

using BusinessRuntimeHttp3SupportProjection = MDRAVA.BLL.ControlPlane.Http3.RuntimeHttp3SupportProjection;

namespace MDRAVA.API.Controllers;

public sealed record RuntimeHttp3SupportResponse(
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

    public IReadOnlyList<string> SupportedRouteActions { get; init; } =
        ["proxy", "redirect", "staticResponse", "maintenance"];

    public IReadOnlyList<string> SupportedPolicyFeatures { get; init; } = [];

    public IReadOnlyList<string> UnsupportedFeatures { get; init; } = [];

    public bool UpstreamHttp3Configured { get; init; }

    public string UpstreamPoolingMode { get; init; } = "not_configured";

    public bool UpstreamMultiplexingEnabled { get; init; }

    public int UpstreamMaxStreamsPerConnection { get; init; } = 8;

    public string UpstreamQpackMode { get; init; } = "static_with_zero_dynamic_table";

    public string UpstreamPoolingLimitationReason { get; init; } = "";

    public static RuntimeHttp3SupportResponse FromProjection(BusinessRuntimeHttp3SupportProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeHttp3SupportResponse(
            projection.RuntimeSupport,
            projection.QuicListenerSupported,
            projection.QuicConnectionSupported,
            projection.Configured,
            projection.EnablementLevel,
            projection.EnabledForTraffic,
            projection.QuicListenerReady,
            projection.AltSvcConfigured,
            projection.AltSvcActive,
            projection.AltSvcMaxAgeSeconds,
            projection.DisabledReason,
            projection.UdpQuicListenerIdentityModeled,
            projection.ReadinessConclusion)
        {
            DefaultEnablementState = projection.DefaultEnablementState,
            DefaultReadinessBlockers = projection.DefaultReadinessBlockers,
            AltSvcStateReason = projection.AltSvcStateReason,
            QpackMode = projection.QpackMode,
            QpackDynamicTableCapacity = projection.QpackDynamicTableCapacity,
            QpackBlockedStreams = projection.QpackBlockedStreams,
            RequestBodyMode = projection.RequestBodyMode,
            ClientHttp3SupportLevel = projection.ClientHttp3SupportLevel,
            UpstreamHttp3SupportLevel = projection.UpstreamHttp3SupportLevel,
            ClientProtocols = projection.ClientProtocols,
            UpstreamProtocols = projection.UpstreamProtocols,
            SupportedRouteActions = projection.SupportedRouteActions,
            SupportedPolicyFeatures = projection.SupportedPolicyFeatures,
            UnsupportedFeatures = projection.UnsupportedFeatures,
            UpstreamHttp3Configured = projection.UpstreamHttp3Configured,
            UpstreamPoolingMode = projection.UpstreamPoolingMode,
            UpstreamMultiplexingEnabled = projection.UpstreamMultiplexingEnabled,
            UpstreamMaxStreamsPerConnection = projection.UpstreamMaxStreamsPerConnection,
            UpstreamQpackMode = projection.UpstreamQpackMode,
            UpstreamPoolingLimitationReason = projection.UpstreamPoolingLimitationReason
        };
    }
}
