using System.Net.Quic;

namespace MDRAVA.API.Proxy.Http3;

public static class Http3RuntimeSupport
{
    public static RuntimeHttp3SupportProjection Project(
        IReadOnlyList<RuntimeListener> listeners,
        IReadOnlyList<ProxyListenerStatus>? runtimeListeners = null,
        IReadOnlyList<RuntimeRoute>? routes = null)
    {
        var previewConfigured = listeners.Any(static listener => listener.Http3.Configured);
        var previewEnabled = listeners.Any(static listener => listener.Http3.EnabledForTraffic);
        var quicReady = runtimeListeners?.Any(static listener =>
            string.Equals(listener.Kind, "quic", StringComparison.OrdinalIgnoreCase)
            && listener.State == ProxyListenerState.Active) ?? false;
        var altSvcConfigured = listeners.Any(static listener => Http3AltSvcPolicy.IsEnabled(listener));
        var altSvcActive = altSvcConfigured
            && runtimeListeners is not null
            && listeners.Any(listener => Http3AltSvcPolicy.IsEnabled(listener) && Http3AltSvcPolicy.HasActiveQuicListener(listener, runtimeListeners));
        var maxAge = listeners
            .Where(static listener => Http3AltSvcPolicy.IsEnabled(listener))
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
        var upstreamHttp3Configured = routes?.Any(static route => route.Upstreams.Any(static upstream =>
            RuntimeUpstreamProtocol.IsHttp3(upstream.Protocol))) ?? false;
        return new RuntimeHttp3SupportProjection(
            support.RuntimeSupport,
            support.QuicListenerSupported,
            support.QuicConnectionSupported,
            ConfiguredMode(listeners),
            EnablementLevel(listeners),
            previewEnabled,
            quicReady,
            altSvcConfigured,
            altSvcActive,
            maxAge,
            DisabledReason(listeners, previewConfigured, previewEnabled, quicReady, runtimeListeners is not null),
            UdpQuicListenerIdentityModeled: true,
            readinessConclusion)
        {
            DefaultEnablementState = defaultState,
            DefaultReadinessBlockers = blockers,
            AltSvcStateReason = AltSvcReason(altSvcConfigured, altSvcActive, previewEnabled, quicReady, runtimeListeners is not null),
            UpstreamHttp3Configured = upstreamHttp3Configured,
            UpstreamPoolingMode = upstreamHttp3Configured ? "one_request_per_connection" : "not_configured",
            UpstreamMultiplexingEnabled = false,
            UpstreamMaxStreamsPerConnection = upstreamHttp3Configured ? 1 : 0,
            UpstreamQpackMode = "static_with_zero_dynamic_table",
            UpstreamPoolingLimitationReason = upstreamHttp3Configured
                ? "upstream_http3_multiplexing_deferred"
                : ""
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
        if (listeners.Any(static listener => listener.Http3.Configured && listener.Http3.EnablementLevel == "beta"))
        {
            return "beta";
        }

        if (listeners.Any(static listener => listener.Http3.Configured && listener.Http3.EnablementLevel == "preview"))
        {
            return "preview";
        }

        return listeners.Any(static listener => listener.Http3.Configured && listener.Http3.EnablementLevel == "default")
            ? "default"
            : "disabled";
    }

    private static string DisabledReason(
        IReadOnlyList<RuntimeListener> listeners,
        bool configured,
        bool enabled,
        bool ready,
        bool hasRuntimeState)
    {
        if (ready)
        {
            return "quic_listener_ready";
        }

        if (enabled && !hasRuntimeState)
        {
            return listeners.Any(static listener => listener.Http3.EnabledForTraffic && listener.Http3.EnablementLevel == "default")
                ? "default_enabled"
                : "preview_enabled";
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

        return blockers.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string ConfiguredMode(IReadOnlyList<RuntimeListener> listeners)
    {
        if (listeners.Any(static listener => listener.Http3.Configured && listener.Http3.EnablementLevel == "beta"))
        {
            return "beta";
        }

        if (listeners.Any(static listener => listener.Http3.Configured && listener.Http3.EnablementLevel == "preview"))
        {
            return "preview";
        }

        return listeners.Any(static listener => listener.Http3.Configured && listener.Http3.EnablementLevel == "default")
            ? "default"
            : "disabled";
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
