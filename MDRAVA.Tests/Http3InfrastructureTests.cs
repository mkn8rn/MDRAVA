using System.Net.Security;
using MDRAVA.API.Proxy.Configuration;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Http3;
using MDRAVA.API.Proxy.Tls;

namespace MDRAVA.Tests;

internal static class Http3InfrastructureTests
{
    public static void ExistingHttp1AndHttp2ProtocolsStillValidate()
    {
        var http1 = new ProxyOptionsValidator().Validate(
            null,
            ValidProxyOptions(
                new ListenerOptions
                {
                    Name = "http1",
                    Address = "127.0.0.1",
                    Port = 8080,
                    Transport = "http",
                    Protocols = "http1"
                }));
        var http2 = new ProxyOptionsValidator().Validate(
            null,
            ValidProxyOptions(
                new ListenerOptions
                {
                    Name = "http2",
                    Address = "127.0.0.1",
                    Port = 8443,
                    Transport = "https",
                    Protocols = "http2"
                }));
        var both = new ProxyOptionsValidator().Validate(
            null,
            ValidProxyOptions(
                new ListenerOptions
                {
                    Name = "both",
                    Address = "127.0.0.1",
                    Port = 9443,
                    Transport = "https",
                    Protocols = "http1AndHttp2"
                }));

        AssertEx.False(http1.Failed, string.Join("; ", http1.Failures ?? []));
        AssertEx.False(http2.Failed, string.Join("; ", http2.Failures ?? []));
        AssertEx.False(both.Failed, string.Join("; ", both.Failures ?? []));
    }

    public static void Http3PreviewIsDisabledByDefault()
    {
        var listener = RuntimeListenerFor("http1");

        AssertEx.False(listener.ExperimentalHttp3);
        AssertEx.False(listener.Http3PreviewConfigured);
        AssertEx.False(listener.Http3.EnabledForTraffic);
        AssertEx.Equal("not_configured", listener.Http3.DisabledReason);
    }

    public static void Http3PreviewConfigIsRejectedWithoutExperimentalGate()
    {
        var validation = new ProxyOptionsValidator().Validate(
            null,
            ValidProxyOptions(Http3Listener("preview", "http1AndHttp2AndHttp3Preview", experimental: false)));

        AssertEx.True(validation.Failed);
        var failures = AssertEx.NotNull(validation.Failures);
        AssertEx.True(
            failures.Any(static failure => failure.Contains("ExperimentalHttp3", StringComparison.Ordinal)),
            string.Join("; ", failures));
    }

    public static void Http3PreviewRequiresTlsCertificateCapableListener()
    {
        var listener = new ListenerOptions
        {
            Name = "preview",
            Address = "127.0.0.1",
            Port = 8443,
            Transport = "https",
            Protocols = "http1AndHttp2AndHttp3Preview",
            ExperimentalHttp3 = true,
            DefaultCertificateId = null,
            SniCertificates = []
        };
        var validation = new ProxyOptionsValidator().Validate(null, ValidProxyOptions(listener));

        AssertEx.True(validation.Failed);
        var failures = AssertEx.NotNull(validation.Failures);
        AssertEx.True(
            failures.Any(static failure => failure.Contains("requires DefaultCertificateId or SniCertificates", StringComparison.Ordinal)),
            string.Join("; ", failures));
    }

    public static void Http3PreviewConfigIsAcceptedWithExplicitGateAndEnablesPreviewTraffic()
    {
        var listener = Http3Listener("preview", "http1AndHttp2AndHttp3Preview", experimental: true);
        var validation = new ProxyOptionsValidator().Validate(null, ValidProxyOptions(listener));
        var runtime = RuntimeListenerFor("http1AndHttp2AndHttp3Preview", experimentalHttp3: true);

        AssertEx.False(validation.Failed, string.Join("; ", validation.Failures ?? []));
        AssertEx.True(runtime.Http3PreviewConfigured);
        AssertEx.True(runtime.TcpTrafficEnabled);
        AssertEx.True(runtime.Http3.EnabledForTraffic);
        AssertEx.Equal("preview_enabled", runtime.Http3.DisabledReason);
    }

    public static void Http3OnlyPreviewDoesNotEnableTcpTraffic()
    {
        var listener = Http3Listener("preview", "http3Preview", experimental: true);
        var validation = new ProxyOptionsValidator().Validate(null, ValidProxyOptions(listener));
        var runtime = RuntimeListenerFor("http3Preview", experimentalHttp3: true);

        AssertEx.False(validation.Failed, string.Join("; ", validation.Failures ?? []));
        AssertEx.True(runtime.Http3PreviewConfigured);
        AssertEx.False(runtime.TcpTrafficEnabled);
        AssertEx.True(runtime.Http3.EnabledForTraffic);
    }

    public static void TcpListenerIdentityRemainsUnchanged()
    {
        var listener = RuntimeListenerFor("http1AndHttp2");
        var identity = listener.Identity;

        AssertEx.Equal("main", identity.Key);
        AssertEx.Equal("127.0.0.1|8443|https", identity.BindKey);
    }

    public static void FutureQuicListenerIdentityIsSeparateFromTcpIdentity()
    {
        var listener = RuntimeListenerFor("http1AndHttp2AndHttp3Preview", experimentalHttp3: true);
        var tcpIdentity = listener.Identity;
        var quicIdentity = AssertEx.NotNull(listener.QuicIdentity);

        AssertEx.Equal("main|quic", quicIdentity.Key);
        AssertEx.Equal("127.0.0.1|8443|udp|quic", quicIdentity.BindKey);
        AssertEx.False(string.Equals(tcpIdentity.BindKey, quicIdentity.BindKey, StringComparison.Ordinal));
    }

    public static void TcpAlpnDoesNotAdvertiseHttp3()
    {
        var protocols = RuntimeListenerProtocols.Http1AndHttp2AndHttp3Preview;
        var tcpAlpn = ListenerProtocolAdvertisement.BuildTcpAlpn(protocols);
        var futureQuicAlpn = ListenerProtocolAdvertisement.FutureHttp3Alpn(protocols);

        AssertEx.True(tcpAlpn.Contains(SslApplicationProtocol.Http2));
        AssertEx.True(tcpAlpn.Contains(SslApplicationProtocol.Http11));
        AssertEx.False(tcpAlpn.Any(static protocol => protocol.Protocol.Span.SequenceEqual("h3"u8)));
        AssertEx.True(futureQuicAlpn.Any(static protocol => protocol.Protocol.Span.SequenceEqual("h3"u8)));
    }

    public static void StatusAndEffectiveProjectionReportHttp3PreviewEnabled()
    {
        var snapshot = ProxyConfigurationMapper.ToRuntimeSnapshot(
            ValidProxyOptions(Http3Listener("preview", "http1AndHttp2AndHttp3Preview", experimental: true)),
            new ProxyOperationalOptions(),
            new Dictionary<string, RuntimeCertificate>(StringComparer.OrdinalIgnoreCase),
            1,
            DateTimeOffset.UtcNow,
            "memory",
            [],
            Discovery());
        var projection = ProxyConfigurationMapper.ToProjection(snapshot);

        AssertEx.Equal("preview", projection.Http3.Configured);
        AssertEx.True(projection.Http3.EnabledForTraffic);
        AssertEx.Equal("preview_enabled", projection.Http3.DisabledReason);
        AssertEx.True(projection.Http3.UdpQuicListenerIdentityModeled);
        var statusProjection = Http3RuntimeSupport.Project(snapshot.Listeners);
        AssertEx.Equal("preview", statusProjection.Configured);
        AssertEx.True(statusProjection.EnabledForTraffic);
        AssertEx.Equal("preview_enabled", statusProjection.DisabledReason);
    }

    public static void UpstreamProtocolStillRejectsHttp3()
    {
        var options = ValidProxyOptions(
            new ListenerOptions
            {
                Name = "main",
                Address = "127.0.0.1",
                Port = 8080,
                Transport = "http",
                Protocols = "http1"
            });
        options.Routes[0].Upstreams[0] = new UpstreamOptions
        {
            Name = "h3",
            Scheme = "https",
            Protocol = "http3",
            Address = "upstream.test",
            Port = 443
        };

        var validation = new ProxyOptionsValidator().Validate(null, options);

        AssertEx.True(validation.Failed);
        var failures = AssertEx.NotNull(validation.Failures);
        AssertEx.True(
            failures.Any(static failure => failure.Contains("Protocol must be 'http1' or 'http2'", StringComparison.Ordinal)),
            string.Join("; ", failures));
    }

    private static ListenerOptions Http3Listener(string name, string protocols, bool experimental)
    {
        return new ListenerOptions
        {
            Name = name,
            Address = "127.0.0.1",
            Port = 8443,
            Transport = "https",
            Protocols = protocols,
            ExperimentalHttp3 = experimental,
            DefaultCertificateId = "default"
        };
    }

    private static RuntimeListener RuntimeListenerFor(
        string protocols,
        bool experimentalHttp3 = false)
    {
        return new RuntimeListener(
            "main",
            "127.0.0.1",
            8443,
            true,
            RuntimeListenerTransport.Https,
            "default",
            [],
            512,
            32 * 1024,
            32 * 1024,
            1024,
            64 * 1024)
        {
            Protocols = ParseProtocols(protocols),
            ExperimentalHttp3 = experimentalHttp3
        };
    }

    private static RuntimeListenerProtocols ParseProtocols(string protocols)
    {
        return protocols.ToLowerInvariant() switch
        {
            "http2" => RuntimeListenerProtocols.Http2,
            "http1andhttp2" => RuntimeListenerProtocols.Http1AndHttp2,
            "http3preview" => RuntimeListenerProtocols.Http3Preview,
            "http1andhttp2andhttp3preview" => RuntimeListenerProtocols.Http1AndHttp2AndHttp3Preview,
            _ => RuntimeListenerProtocols.Http1
        };
    }

    private static ProxyOptions ValidProxyOptions(ListenerOptions listener)
    {
        return new ProxyOptions
        {
            Listeners = [listener],
            Routes =
            [
                new ProxyRouteOptions
                {
                    Name = "proxy",
                    Host = "*",
                    PathPrefix = "/",
                    Upstreams =
                    [
                        new UpstreamOptions
                        {
                            Name = "local",
                            Address = "127.0.0.1",
                            Port = 5000
                        }
                    ]
                }
            ]
        };
    }

    private static ProxyConfigurationDiscovery Discovery()
    {
        return new ProxyConfigurationDiscovery(
            new ProxyFilesystemLayout(
                "tests",
                "tests/config",
                "tests/config/sites",
                "tests/logs",
                "tests/certs",
                "tests/state",
                "tests/config/proxy.json"),
            [],
            [],
            []);
    }
}
