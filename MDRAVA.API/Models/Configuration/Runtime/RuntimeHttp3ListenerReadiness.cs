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
        var http3ProtocolConfigured = listener.Protocols.HasHttp3();
        var certificateCapable = !string.IsNullOrWhiteSpace(listener.DefaultCertificateId)
            || listener.SniCertificates.Count > 0;
        var enablement = RuntimeHttp3Compatibility.ResolveEffectiveEnablement(
            listener.Protocols,
            listener.ExperimentalHttp3,
            listener.Http3Enablement);
        var configured = enablement != RuntimeHttp3Enablement.Disabled
            && listener.Transport == RuntimeListenerTransport.Https
            && certificateCapable;
        var enabledForTraffic = configured
            && enablement != RuntimeHttp3Enablement.Disabled
            && listener.Transport == RuntimeListenerTransport.Https
            && certificateCapable;
        var reason = DisabledReasonFor(listener, http3ProtocolConfigured, configured, certificateCapable, enablement, enabledForTraffic);

        return new RuntimeHttp3ListenerReadiness(
            configured,
            listener.ExperimentalHttp3 || enablement == RuntimeHttp3Enablement.Default,
            enablement.ToConfigText(),
            enabledForTraffic,
            reason,
            enabledForTraffic || listener.Http3AltSvc.Enabled,
            listener.Http3AltSvc.MaxAgeSeconds,
            configured,
            enabledForTraffic ? RuntimeQuicListenerIdentity.From(listener) : null);
    }

    private static string DisabledReasonFor(
        RuntimeListener listener,
        bool http3ProtocolConfigured,
        bool configured,
        bool certificateCapable,
        RuntimeHttp3Enablement enablement,
        bool enabledForTraffic)
    {
        if (enabledForTraffic)
        {
            return enablement switch
            {
                RuntimeHttp3Enablement.Beta => "beta_enabled",
                RuntimeHttp3Enablement.Preview => "preview_enabled",
                _ => "default_enabled"
            };
        }

        if (enablement == RuntimeHttp3Enablement.Disabled)
        {
            return "disabled";
        }

        if (http3ProtocolConfigured && !listener.ExperimentalHttp3)
        {
            return "experimental_gate_missing";
        }

        if (listener.Transport != RuntimeListenerTransport.Https)
        {
            return "tls_required";
        }

        if (!certificateCapable)
        {
            return "certificate_required";
        }

        return configured ? "configured_but_inactive" : "not_configured";
    }
}
