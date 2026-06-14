namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeHttp3ListenerReadinessProjection(
    bool Configured,
    bool DefaultEnabled,
    string EnablementLevel,
    bool EnabledForTraffic,
    string DisabledReason,
    bool AltSvcConfigured,
    int AltSvcMaxAgeSeconds,
    bool UdpQuicListenerIdentityModeled,
    RuntimeQuicListenerIdentityProjection? QuicIdentity);
