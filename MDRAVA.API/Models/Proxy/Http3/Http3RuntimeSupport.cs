using System.Net.Quic;

namespace MDRAVA.API.Proxy.Http3;

public static class Http3RuntimeSupport
{
    public static RuntimeHttp3SupportProjection Project(
        IReadOnlyList<RuntimeListener> listeners,
        IReadOnlyList<ProxyListenerStatus>? runtimeListeners = null)
    {
        var previewConfigured = listeners.Any(static listener => listener.Http3PreviewConfigured);
        var previewEnabled = listeners.Any(static listener => listener.Http3.EnabledForTraffic);
        var quicReady = runtimeListeners?.Any(static listener =>
            string.Equals(listener.Kind, "quic", StringComparison.OrdinalIgnoreCase)
            && listener.State == ProxyListenerState.Active) ?? false;
        var altSvcConfigured = listeners.Any(static listener => listener.Http3AltSvc.Enabled);
        var altSvcActive = altSvcConfigured
            && runtimeListeners is not null
            && listeners.Any(listener => listener.Http3AltSvc.Enabled && Http3AltSvcPolicy.HasActiveQuicListener(listener, runtimeListeners));
        var maxAge = listeners
            .Where(static listener => listener.Http3AltSvc.Enabled)
            .Select(static listener => (int?)listener.Http3AltSvc.MaxAgeSeconds)
            .FirstOrDefault();
        var support = Check();
        var blockers = DefaultReadinessBlockers(listeners, support);
        var defaultState = !previewConfigured
            ? "disabled"
            : blockers.Count == 0 && previewEnabled
                ? "default-capable"
                : "explicit";
        var readinessConclusion = defaultState == "default-capable"
            ? "default_capable_when_config_default_flips"
            : defaultState == "disabled"
                ? "disabled"
                : "explicit_only";
        return new RuntimeHttp3SupportProjection(
            support.RuntimeSupport,
            support.QuicListenerSupported,
            support.QuicConnectionSupported,
            previewConfigured ? "preview" : "disabled",
            EnablementLevel(listeners),
            previewEnabled,
            quicReady,
            altSvcConfigured,
            altSvcActive,
            maxAge,
            DisabledReason(previewConfigured, previewEnabled, quicReady, runtimeListeners is not null),
            UdpQuicListenerIdentityModeled: true,
            readinessConclusion)
        {
            DefaultEnablementState = defaultState,
            DefaultReadinessBlockers = blockers,
            AltSvcStateReason = AltSvcReason(altSvcConfigured, altSvcActive, previewEnabled, quicReady, runtimeListeners is not null)
        };
    }

    private static RuntimeHttp3RuntimeSupport Check()
    {
        try
        {
            var listenerSupported = QuicListener.IsSupported;
            var connectionSupported = QuicConnection.IsSupported;
            return new RuntimeHttp3RuntimeSupport(
                listenerSupported && connectionSupported ? "supported" : "unsupported",
                listenerSupported,
                connectionSupported);
        }
        catch
        {
            return new RuntimeHttp3RuntimeSupport(
                "unknown",
                QuicListenerSupported: false,
                QuicConnectionSupported: false);
        }
    }

    private sealed record RuntimeHttp3RuntimeSupport(
        string RuntimeSupport,
        bool QuicListenerSupported,
        bool QuicConnectionSupported);

    private static string EnablementLevel(IReadOnlyList<RuntimeListener> listeners)
    {
        if (listeners.Any(static listener => listener.Http3.EnablementLevel == "beta"))
        {
            return "beta";
        }

        return listeners.Any(static listener => listener.Http3.EnablementLevel == "preview")
            ? "preview"
            : "disabled";
    }

    private static string DisabledReason(bool configured, bool enabled, bool ready, bool hasRuntimeState)
    {
        if (ready)
        {
            return "quic_listener_ready";
        }

        if (enabled && !hasRuntimeState)
        {
            return "preview_enabled";
        }

        if (enabled)
        {
            return "configured_but_listener_not_ready";
        }

        return configured ? "preview_configured_but_inactive" : "not_configured";
    }

    private static IReadOnlyList<string> DefaultReadinessBlockers(
        IReadOnlyList<RuntimeListener> listeners,
        RuntimeHttp3RuntimeSupport support)
    {
        List<string> blockers = [];
        if (!support.QuicListenerSupported || !support.QuicConnectionSupported)
        {
            blockers.Add("runtime_quic_unsupported");
        }

        if (!listeners.Any(static listener => listener.Http3.EnabledForTraffic))
        {
            blockers.Add("no_http3_enabled_listener");
        }

        if (listeners.Any(static listener => listener.Http3.EnabledForTraffic && listener.Http3MaxBufferedRequestBodyBytes > 0))
        {
            blockers.Add("request_body_buffered_not_streamed");
        }

        blockers.Add("qpack_dynamic_table_unsupported");
        return blockers.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string AltSvcReason(
        bool configured,
        bool active,
        bool enabled,
        bool ready,
        bool hasRuntimeState)
    {
        if (active)
        {
            return "active";
        }

        if (!configured)
        {
            return "not_configured";
        }

        if (!enabled)
        {
            return "http3_not_enabled";
        }

        if (!hasRuntimeState)
        {
            return "runtime_state_unavailable";
        }

        return ready ? "listener_not_matched" : "quic_listener_not_ready";
    }
}
