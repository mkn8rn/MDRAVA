using BusinessRuntimeHttp3SupportProjection = MDRAVA.BLL.ControlPlane.Http3.RuntimeHttp3SupportProjection;

namespace MDRAVA.API.Controllers;

public sealed record RuntimeHttp3SupportResponse
{
    public RuntimeHttp3SupportResponse(
        string runtimeSupport,
        bool quicListenerSupported,
        bool quicConnectionSupported,
        string configured,
        string enablementLevel,
        bool enabledForTraffic,
        bool quicListenerReady,
        bool altSvcConfigured,
        bool altSvcActive,
        int? altSvcMaxAgeSeconds,
        string disabledReason,
        bool udpQuicListenerIdentityModeled,
        string readinessConclusion)
        : this(
            runtimeSupport,
            quicListenerSupported,
            quicConnectionSupported,
            configured,
            enablementLevel,
            enabledForTraffic,
            quicListenerReady,
            altSvcConfigured,
            altSvcActive,
            altSvcMaxAgeSeconds,
            disabledReason,
            udpQuicListenerIdentityModeled,
            readinessConclusion,
            defaultEnablementState: "disabled",
            defaultReadinessBlockers: [],
            altSvcStateReason: "not_configured",
            qpackMode: "static_with_zero_dynamic_table",
            qpackDynamicTableCapacity: 0,
            qpackBlockedStreams: 0,
            requestBodyMode: "streaming",
            clientHttp3SupportLevel: "default_enabled_for_eligible_tls_proxy_listeners",
            upstreamHttp3SupportLevel: "opt_in_https_quic",
            clientProtocols: ["http1", "http2", "http3"],
            upstreamProtocols: ["http1", "http2", "http3"],
            supportedRouteActions: ["proxy", "redirect", "staticResponse", "maintenance"],
            supportedPolicyFeatures: [],
            unsupportedFeatures: [],
            upstreamHttp3Configured: false,
            upstreamPoolingMode: "not_configured",
            upstreamMultiplexingEnabled: false,
            upstreamMaxStreamsPerConnection: 8,
            upstreamQpackMode: "static_with_zero_dynamic_table",
            upstreamPoolingLimitationReason: "")
    {
    }

    public RuntimeHttp3SupportResponse(
        string runtimeSupport,
        bool quicListenerSupported,
        bool quicConnectionSupported,
        string configured,
        string enablementLevel,
        bool enabledForTraffic,
        bool quicListenerReady,
        bool altSvcConfigured,
        bool altSvcActive,
        int? altSvcMaxAgeSeconds,
        string disabledReason,
        bool udpQuicListenerIdentityModeled,
        string readinessConclusion,
        string defaultEnablementState,
        IReadOnlyList<string> defaultReadinessBlockers,
        string altSvcStateReason,
        string qpackMode,
        int qpackDynamicTableCapacity,
        int qpackBlockedStreams,
        string requestBodyMode,
        string clientHttp3SupportLevel,
        string upstreamHttp3SupportLevel,
        IReadOnlyList<string> clientProtocols,
        IReadOnlyList<string> upstreamProtocols,
        IReadOnlyList<string> supportedRouteActions,
        IReadOnlyList<string> supportedPolicyFeatures,
        IReadOnlyList<string> unsupportedFeatures,
        bool upstreamHttp3Configured,
        string upstreamPoolingMode,
        bool upstreamMultiplexingEnabled,
        int upstreamMaxStreamsPerConnection,
        string upstreamQpackMode,
        string upstreamPoolingLimitationReason)
    {
        ArgumentNullException.ThrowIfNull(runtimeSupport);
        ArgumentNullException.ThrowIfNull(configured);
        ArgumentNullException.ThrowIfNull(enablementLevel);
        ArgumentNullException.ThrowIfNull(disabledReason);
        ArgumentNullException.ThrowIfNull(readinessConclusion);
        ArgumentNullException.ThrowIfNull(defaultEnablementState);
        ArgumentNullException.ThrowIfNull(defaultReadinessBlockers);
        ArgumentNullException.ThrowIfNull(altSvcStateReason);
        ArgumentNullException.ThrowIfNull(qpackMode);
        ArgumentNullException.ThrowIfNull(requestBodyMode);
        ArgumentNullException.ThrowIfNull(clientHttp3SupportLevel);
        ArgumentNullException.ThrowIfNull(upstreamHttp3SupportLevel);
        ArgumentNullException.ThrowIfNull(clientProtocols);
        ArgumentNullException.ThrowIfNull(upstreamProtocols);
        ArgumentNullException.ThrowIfNull(supportedRouteActions);
        ArgumentNullException.ThrowIfNull(supportedPolicyFeatures);
        ArgumentNullException.ThrowIfNull(unsupportedFeatures);
        ArgumentNullException.ThrowIfNull(upstreamPoolingMode);
        ArgumentNullException.ThrowIfNull(upstreamQpackMode);
        ArgumentNullException.ThrowIfNull(upstreamPoolingLimitationReason);

        RuntimeSupport = runtimeSupport;
        QuicListenerSupported = quicListenerSupported;
        QuicConnectionSupported = quicConnectionSupported;
        Configured = configured;
        EnablementLevel = enablementLevel;
        EnabledForTraffic = enabledForTraffic;
        QuicListenerReady = quicListenerReady;
        AltSvcConfigured = altSvcConfigured;
        AltSvcActive = altSvcActive;
        AltSvcMaxAgeSeconds = altSvcMaxAgeSeconds;
        DisabledReason = disabledReason;
        UdpQuicListenerIdentityModeled = udpQuicListenerIdentityModeled;
        ReadinessConclusion = readinessConclusion;
        DefaultEnablementState = defaultEnablementState;
        DefaultReadinessBlockers = ApiResponseList.Copy(defaultReadinessBlockers);
        AltSvcStateReason = altSvcStateReason;
        QpackMode = qpackMode;
        QpackDynamicTableCapacity = qpackDynamicTableCapacity;
        QpackBlockedStreams = qpackBlockedStreams;
        RequestBodyMode = requestBodyMode;
        ClientHttp3SupportLevel = clientHttp3SupportLevel;
        UpstreamHttp3SupportLevel = upstreamHttp3SupportLevel;
        ClientProtocols = ApiResponseList.Copy(clientProtocols);
        UpstreamProtocols = ApiResponseList.Copy(upstreamProtocols);
        SupportedRouteActions = ApiResponseList.Copy(supportedRouteActions);
        SupportedPolicyFeatures = ApiResponseList.Copy(supportedPolicyFeatures);
        UnsupportedFeatures = ApiResponseList.Copy(unsupportedFeatures);
        UpstreamHttp3Configured = upstreamHttp3Configured;
        UpstreamPoolingMode = upstreamPoolingMode;
        UpstreamMultiplexingEnabled = upstreamMultiplexingEnabled;
        UpstreamMaxStreamsPerConnection = upstreamMaxStreamsPerConnection;
        UpstreamQpackMode = upstreamQpackMode;
        UpstreamPoolingLimitationReason = upstreamPoolingLimitationReason;
    }

    public string RuntimeSupport { get; }

    public bool QuicListenerSupported { get; }

    public bool QuicConnectionSupported { get; }

    public string Configured { get; }

    public string EnablementLevel { get; }

    public bool EnabledForTraffic { get; }

    public bool QuicListenerReady { get; }

    public bool AltSvcConfigured { get; }

    public bool AltSvcActive { get; }

    public int? AltSvcMaxAgeSeconds { get; }

    public string DisabledReason { get; }

    public bool UdpQuicListenerIdentityModeled { get; }

    public string ReadinessConclusion { get; }

    public string DefaultEnablementState { get; }

    public IReadOnlyList<string> DefaultReadinessBlockers { get; }

    public string AltSvcStateReason { get; }

    public string QpackMode { get; }

    public int QpackDynamicTableCapacity { get; }

    public int QpackBlockedStreams { get; }

    public string RequestBodyMode { get; }

    public string ClientHttp3SupportLevel { get; }

    public string UpstreamHttp3SupportLevel { get; }

    public IReadOnlyList<string> ClientProtocols { get; }

    public IReadOnlyList<string> UpstreamProtocols { get; }

    public IReadOnlyList<string> SupportedRouteActions { get; }

    public IReadOnlyList<string> SupportedPolicyFeatures { get; }

    public IReadOnlyList<string> UnsupportedFeatures { get; }

    public bool UpstreamHttp3Configured { get; }

    public string UpstreamPoolingMode { get; }

    public bool UpstreamMultiplexingEnabled { get; }

    public int UpstreamMaxStreamsPerConnection { get; }

    public string UpstreamQpackMode { get; }

    public string UpstreamPoolingLimitationReason { get; }

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
            projection.ReadinessConclusion,
            projection.DefaultEnablementState,
            projection.DefaultReadinessBlockers,
            projection.AltSvcStateReason,
            projection.QpackMode,
            projection.QpackDynamicTableCapacity,
            projection.QpackBlockedStreams,
            projection.RequestBodyMode,
            projection.ClientHttp3SupportLevel,
            projection.UpstreamHttp3SupportLevel,
            projection.ClientProtocols,
            projection.UpstreamProtocols,
            projection.SupportedRouteActions,
            projection.SupportedPolicyFeatures,
            projection.UnsupportedFeatures,
            projection.UpstreamHttp3Configured,
            projection.UpstreamPoolingMode,
            projection.UpstreamMultiplexingEnabled,
            projection.UpstreamMaxStreamsPerConnection,
            projection.UpstreamQpackMode,
            projection.UpstreamPoolingLimitationReason);
    }
}
