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
        var legacyPreviewConfigured = listener.Protocols.HasHttp3Preview();
        var certificateCapable = !string.IsNullOrWhiteSpace(listener.DefaultCertificateId)
            || listener.SniCertificates.Count > 0;
        var enablement = EffectiveEnablement(listener, legacyPreviewConfigured);
        var configured = enablement != RuntimeHttp3Enablement.Disabled
            && listener.Transport == RuntimeListenerTransport.Https
            && certificateCapable;
        var enabledForTraffic = configured
            && enablement != RuntimeHttp3Enablement.Disabled
            && listener.Transport == RuntimeListenerTransport.Https
            && certificateCapable;
        var reason = DisabledReasonFor(listener, legacyPreviewConfigured, configured, certificateCapable, enablement, enabledForTraffic);

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
        bool legacyPreviewConfigured,
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

        if (legacyPreviewConfigured && !listener.ExperimentalHttp3)
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

    private static RuntimeHttp3Enablement EffectiveEnablement(RuntimeListener listener, bool configured)
    {
        return listener.Http3Enablement switch
        {
            RuntimeHttp3Enablement.Disabled => RuntimeHttp3Enablement.Disabled,
            RuntimeHttp3Enablement.Preview or RuntimeHttp3Enablement.Beta => listener.Http3Enablement,
            _ => configured && listener.ExperimentalHttp3
                ? RuntimeHttp3Enablement.Preview
                : RuntimeHttp3Enablement.Default
        };
    }
}
