namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeHttp3ListenerReadiness(
    bool Configured,
    bool ExperimentalGateEnabled,
    string EnablementLevel,
    bool EnabledForTraffic,
    string DisabledReason,
    bool AltSvcConfigured,
    int AltSvcMaxAgeSeconds,
    bool UdpQuicListenerIdentityModeled,
    RuntimeQuicListenerIdentity? QuicIdentity)
{
    public static RuntimeHttp3ListenerReadiness From(RuntimeListener listener)
    {
        var configured = listener.Protocols.HasHttp3Preview();
        var certificateCapable = !string.IsNullOrWhiteSpace(listener.DefaultCertificateId)
            || listener.SniCertificates.Count > 0;
        var enablement = EffectiveEnablement(listener, configured);
        var enabledForTraffic = configured
            && listener.ExperimentalHttp3
            && enablement != RuntimeHttp3Enablement.Disabled
            && listener.Transport == RuntimeListenerTransport.Https
            && certificateCapable;
        var reason = DisabledReasonFor(listener, configured, certificateCapable, enablement, enabledForTraffic);

        return new RuntimeHttp3ListenerReadiness(
            configured,
            listener.ExperimentalHttp3,
            enablement.ToConfigText(),
            enabledForTraffic,
            reason,
            listener.Http3AltSvc.Enabled,
            listener.Http3AltSvc.MaxAgeSeconds,
            configured,
            configured ? RuntimeQuicListenerIdentity.From(listener) : null);
    }

    private static string DisabledReasonFor(
        RuntimeListener listener,
        bool configured,
        bool certificateCapable,
        RuntimeHttp3Enablement enablement,
        bool enabledForTraffic)
    {
        if (enabledForTraffic)
        {
            return enablement == RuntimeHttp3Enablement.Beta
                ? "beta_enabled"
                : "preview_enabled";
        }

        if (!configured)
        {
            return "not_configured";
        }

        if (enablement == RuntimeHttp3Enablement.Disabled)
        {
            return "disabled";
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

    private static RuntimeHttp3Enablement EffectiveEnablement(RuntimeListener listener, bool configured)
    {
        return listener.Http3Enablement != RuntimeHttp3Enablement.Disabled
            ? listener.Http3Enablement
            : configured && listener.ExperimentalHttp3
                ? RuntimeHttp3Enablement.Preview
                : RuntimeHttp3Enablement.Disabled;
    }
}
