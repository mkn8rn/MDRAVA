namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeHttp3SupportProjection(
    string RuntimeSupport,
    bool QuicListenerSupported,
    bool QuicConnectionSupported,
    string Configured,
    bool EnabledForTraffic,
    string DisabledReason,
    bool UdpQuicListenerIdentityModeled);
