using System.Net.Quic;

namespace MDRAVA.API.Proxy.Http3;

public static class Http3RuntimeSupport
{
    public static RuntimeHttp3SupportProjection Project(IReadOnlyList<RuntimeListener> listeners)
    {
        var previewConfigured = listeners.Any(static listener => listener.Http3PreviewConfigured);
        var previewEnabled = listeners.Any(static listener => listener.Http3.EnabledForTraffic);
        var support = Check();
        return new RuntimeHttp3SupportProjection(
            support.RuntimeSupport,
            support.QuicListenerSupported,
            support.QuicConnectionSupported,
            previewConfigured ? "preview" : "disabled",
            previewEnabled,
            previewEnabled ? "preview_enabled" : previewConfigured ? "preview_configured_but_inactive" : "not_configured",
            UdpQuicListenerIdentityModeled: true);
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
}
