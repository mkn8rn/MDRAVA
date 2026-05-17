namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeHttp3ListenerReadiness(
    bool Configured,
    bool ExperimentalGateEnabled,
    bool EnabledForTraffic,
    string DisabledReason,
    bool UdpQuicListenerIdentityModeled,
    RuntimeQuicListenerIdentity? QuicIdentity)
{
    public static RuntimeHttp3ListenerReadiness From(RuntimeListener listener)
    {
        var configured = listener.Protocols.HasHttp3Preview();
        return new RuntimeHttp3ListenerReadiness(
            configured,
            listener.ExperimentalHttp3,
            EnabledForTraffic: false,
            configured ? "request_handling_not_implemented" : "not_configured",
            configured,
            configured ? RuntimeQuicListenerIdentity.From(listener) : null);
    }
}
