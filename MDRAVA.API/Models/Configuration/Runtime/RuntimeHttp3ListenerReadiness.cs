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
        var certificateCapable = !string.IsNullOrWhiteSpace(listener.DefaultCertificateId)
            || listener.SniCertificates.Count > 0;
        var enabledForTraffic = configured
            && listener.ExperimentalHttp3
            && listener.Transport == RuntimeListenerTransport.Https
            && certificateCapable;
        var reason = DisabledReasonFor(listener, configured, certificateCapable, enabledForTraffic);

        return new RuntimeHttp3ListenerReadiness(
            configured,
            listener.ExperimentalHttp3,
            enabledForTraffic,
            reason,
            configured,
            configured ? RuntimeQuicListenerIdentity.From(listener) : null);
    }

    private static string DisabledReasonFor(
        RuntimeListener listener,
        bool configured,
        bool certificateCapable,
        bool enabledForTraffic)
    {
        if (enabledForTraffic)
        {
            return "preview_enabled";
        }

        if (!configured)
        {
            return "not_configured";
        }

        if (!listener.ExperimentalHttp3)
        {
            return "experimental_gate_missing";
        }

        if (listener.Transport != RuntimeListenerTransport.Https)
        {
            return "tls_required";
        }

        return certificateCapable
            ? "preview_disabled"
            : "certificate_required";
    }
}
