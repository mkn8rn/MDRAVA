using System.Net.Quic;
using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane;

public static class Http3RuntimeSupport
{
    public static RuntimeHttp3SupportProjection Project(
        IReadOnlyList<RuntimeListener> listeners,
        IReadOnlyList<ProxyListenerStatus>? runtimeListeners = null,
        IReadOnlyList<RuntimeRoute>? routes = null)
    {
        var http3Configured = listeners.Any(static listener => listener.Http3.Configured);
        var http3Enabled = listeners.Any(static listener => listener.Http3.EnabledForTraffic);
        var quicReady = runtimeListeners?.Any(static listener =>
            string.Equals(listener.Kind, "quic", StringComparison.OrdinalIgnoreCase)
            && listener.State == ProxyListenerState.Active) ?? false;
        var altSvcConfigured = listeners.Any(static listener => RuntimeHttp3AltSvcPolicy.IsEnabled(listener));
        var altSvcActive = altSvcConfigured
            && runtimeListeners is not null
            && listeners.Any(listener => RuntimeHttp3AltSvcPolicy.IsEnabled(listener) && RuntimeHttp3AltSvcPolicy.HasActiveQuicListener(listener, runtimeListeners));
        var maxAge = listeners
            .Where(static listener => RuntimeHttp3AltSvcPolicy.IsEnabled(listener))
            .Select(static listener => (int?)listener.Http3AltSvc.MaxAgeSeconds)
            .FirstOrDefault();
        var support = Check();
        var blockers = DefaultReadinessBlockers(listeners, support);
        var defaultState = !http3Configured
            ? "disabled"
            : blockers.Count == 0 && http3Enabled
                ? "default-enabled"
                : "explicit";
        var readinessConclusion = defaultState == "default-enabled"
            ? "default_enabled_for_eligible_tls_proxy_listeners"
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
            http3Enabled,
            quicReady,
            altSvcConfigured,
            altSvcActive,
            maxAge,
            DisabledReason(listeners, http3Configured, http3Enabled, quicReady, runtimeListeners is not null),
            UdpQuicListenerIdentityModeled: true,
            readinessConclusion)
        {
            DefaultEnablementState = defaultState,
            DefaultReadinessBlockers = blockers,
            AltSvcStateReason = AltSvcReason(altSvcConfigured, altSvcActive, http3Enabled, quicReady, runtimeListeners is not null),
            UpstreamHttp3SupportLevel = upstreamHttp3Configured
                ? "opt_in_https_quic_reused_multiplexed"
                : "opt_in_https_quic_available",
            UpstreamHttp3Configured = upstreamHttp3Configured,
            UpstreamPoolingMode = upstreamHttp3Configured ? "reused_multiplexed" : "not_configured",
            UpstreamMultiplexingEnabled = upstreamHttp3Configured,
            UpstreamMaxStreamsPerConnection = upstreamHttp3Configured ? 8 : 0,
            UpstreamQpackMode = "static_with_zero_dynamic_table",
            UpstreamPoolingLimitationReason = ""
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
            return "default_enabled";
        }

        if (enabled)
        {
            return "configured_but_listener_not_ready";
        }

        return configured ? "configured_but_inactive" : "not_configured";
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
