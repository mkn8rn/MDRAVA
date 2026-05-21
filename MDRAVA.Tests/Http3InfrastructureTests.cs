using System.Net.Security;
using System.Net.Quic;
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

    public static void ListenerProtocolConfigParsingPreservesCompatibility()
    {
        var cases = new (string Text, RuntimeListenerProtocols Protocols, string CanonicalText, bool StableAlias, bool LegacyAlias)[]
        {
            ("http1", RuntimeListenerProtocols.Http1, "http1", false, false),
            ("http2", RuntimeListenerProtocols.Http2, "http2", false, false),
            ("http1AndHttp2", RuntimeListenerProtocols.Http1AndHttp2, "http1AndHttp2", false, false),
            ("http3", RuntimeListenerProtocols.Http3Preview, "http3Preview", true, false),
            ("http1AndHttp3", RuntimeListenerProtocols.Http1AndHttp3Preview, "http1AndHttp3Preview", true, false),
            ("http2AndHttp3", RuntimeListenerProtocols.Http2AndHttp3Preview, "http2AndHttp3Preview", true, false),
            ("http1AndHttp2AndHttp3", RuntimeListenerProtocols.Http1AndHttp2AndHttp3Preview, "http1AndHttp2AndHttp3Preview", true, false),
            ("http3Preview", RuntimeListenerProtocols.Http3Preview, "http3Preview", false, true),
            ("http1AndHttp3Preview", RuntimeListenerProtocols.Http1AndHttp3Preview, "http1AndHttp3Preview", false, true),
            ("http2AndHttp3Preview", RuntimeListenerProtocols.Http2AndHttp3Preview, "http2AndHttp3Preview", false, true),
            ("http1AndHttp2AndHttp3Preview", RuntimeListenerProtocols.Http1AndHttp2AndHttp3Preview, "http1AndHttp2AndHttp3Preview", false, true)
        };

        foreach (var entry in cases)
        {
            var parsed = RuntimeListenerProtocolExtensions.TryParseConfigText(entry.Text, out var protocols);
            var compatibilityParsed = RuntimeHttp3Compatibility.TryParseProtocols(
                entry.Text,
                out var compatibilityProtocols,
                out var stableAlias,
                out var legacyAlias);

            AssertEx.True(parsed, entry.Text);
            AssertEx.True(compatibilityParsed, entry.Text);
            AssertEx.Equal(entry.Protocols, protocols);
            AssertEx.Equal(entry.Protocols, compatibilityProtocols);
            AssertEx.Equal(entry.StableAlias, stableAlias);
            AssertEx.Equal(entry.LegacyAlias, legacyAlias);
            AssertEx.Equal(entry.CanonicalText, protocols.ToConfigText());
        }

        AssertEx.True(RuntimeListenerProtocols.Http1AndHttp2AndHttp3Preview.HasHttp3());
        AssertEx.Equal(
            RuntimeListenerProtocolExtensions.SupportedConfigValues.Count,
            cases.Length);
    }

    public static void Http3CompatibilityNormalizerCentralizesEnablementSemantics()
    {
        var stable = RuntimeHttp3Compatibility.From(Http3Listener("stable", "http1AndHttp3", experimental: false));
        var legacy = RuntimeHttp3Compatibility.From(Http3Listener("legacy", "http1AndHttp3Preview", experimental: true));
        var beta = new ListenerOptions
        {
            Name = "beta",
            Address = "127.0.0.1",
            Port = 8443,
            Transport = "https",
            Protocols = "http3Preview",
            ExperimentalHttp3 = true,
            Http3Enablement = "beta",
            Http3MaxBufferedRequestBodyBytes = 4096,
            DefaultCertificateId = "default"
        };
        var betaCompatibility = RuntimeHttp3Compatibility.From(beta);

        AssertEx.True(stable.ProtocolsValid);
        AssertEx.True(stable.StableProtocolAliasUsed);
        AssertEx.False(stable.LegacyAliasUsed);
        AssertEx.Equal(RuntimeHttp3Enablement.Default, stable.EffectiveEnablement);
        AssertEx.True(stable.ExplicitHttp3Requested);
        AssertEx.True(legacy.LegacyAliasUsed);
        AssertEx.Equal(RuntimeHttp3Enablement.Preview, legacy.EffectiveEnablement);
        AssertEx.True(betaCompatibility.LegacyEnablementAliasUsed);
        AssertEx.True(betaCompatibility.LegacyBufferedRequestBodyLimitConfigured);
        AssertEx.Equal(RuntimeHttp3Enablement.Beta, betaCompatibility.EffectiveEnablement);
    }

    public static void StableHttp3AliasesValidateMapAndAggregateConsistently()
    {
        var stableListener = Http3Listener("main", "http1AndHttp2AndHttp3", experimental: false);
        var validation = new ProxyOptionsValidator().Validate(null, ValidProxyOptions(stableListener));
        var snapshot = ProxyConfigurationMapper.ToRuntimeSnapshot(
            ValidProxyOptions(stableListener),
            new ProxyOperationalOptions(),
            new Dictionary<string, RuntimeCertificate>(StringComparer.OrdinalIgnoreCase),
            1,
            DateTimeOffset.UtcNow,
            "memory",
            [],
            Discovery());

        var aggregated = SiteOptionsAggregator.ToProxyOptions(
            [
                new SiteConfigurationSource(
                    "stable",
                    new SiteOptions
                    {
                        Name = "stable",
                        Host = "stable.test",
                        Upstreams =
                        [
                            new UpstreamOptions
                            {
                                Name = "local",
                                Address = "127.0.0.1",
                                Port = 5000
                            }
                        ],
                        Listeners =
                        [
                            stableListener
                        ]
                    }),
                new SiteConfigurationSource(
                    "stable2",
                    new SiteOptions
                    {
                        Name = "stable2",
                        Host = "stable2.test",
                        Upstreams =
                        [
                            new UpstreamOptions
                            {
                                Name = "local",
                                Address = "127.0.0.1",
                                Port = 5001
                            }
                        ],
                        Listeners =
                        [
                            new ListenerOptions
                            {
                                Name = "main",
                                Address = "127.0.0.1",
                                Port = 8443,
                                Transport = "https",
                                Protocols = "http1AndHttp2",
                                DefaultCertificateId = "default"
                            }
                        ]
                    })
            ]);
        var aggregateValidation = new ProxyOptionsValidator().Validate(null, aggregated);

        AssertEx.False(validation.Failed, string.Join("; ", validation.Failures ?? []));
        AssertEx.Equal(RuntimeListenerProtocols.Http1AndHttp2AndHttp3Preview, snapshot.Listeners[0].Protocols);
        AssertEx.Equal("default", snapshot.Listeners[0].Http3.EnablementLevel);
        AssertEx.True(snapshot.Listeners[0].Http3.EnabledForTraffic);
        AssertEx.False(aggregateValidation.Failed, string.Join("; ", aggregateValidation.Failures ?? []));
        AssertEx.Equal("http1AndHttp2AndHttp3Preview", aggregated.Listeners[0].Protocols);
    }

    public static void Http3DefaultEnabledForEligibleTlsListener()
    {
        var listener = RuntimeListenerFor("http1");

        AssertEx.False(listener.ExperimentalHttp3);
        AssertEx.False(listener.Http3PreviewConfigured);
        AssertEx.True(listener.Http3.Configured);
        AssertEx.True(listener.Http3.EnabledForTraffic);
        AssertEx.Equal("default", listener.Http3.EnablementLevel);
        AssertEx.Equal("default_enabled", listener.Http3.DisabledReason);
    }

    public static void Http3DefaultDisabledForPlaintextListener()
    {
        var listener = new RuntimeListener(
            "main",
            "127.0.0.1",
            8080,
            true,
            RuntimeListenerTransport.Http,
            null,
            [],
            512,
            32 * 1024,
            32 * 1024,
            1024,
            64 * 1024);

        AssertEx.False(listener.Http3.Configured);
        AssertEx.False(listener.Http3.EnabledForTraffic);
        AssertEx.Equal("tls_required", listener.Http3.DisabledReason);
    }

    public static void Http3PreviewProtocolDoesNotRequireExperimentalGateForDefaultEnablement()
    {
        var validation = new ProxyOptionsValidator().Validate(
            null,
            ValidProxyOptions(Http3Listener("preview", "http1AndHttp2AndHttp3Preview", experimental: false)));
        var runtime = RuntimeListenerFor("http1AndHttp2AndHttp3Preview", experimentalHttp3: false);

        AssertEx.False(validation.Failed, string.Join("; ", validation.Failures ?? []));
        AssertEx.True(runtime.Http3PreviewConfigured);
        AssertEx.True(runtime.Http3.EnabledForTraffic);
        AssertEx.Equal("default", runtime.Http3.EnablementLevel);
        AssertEx.Equal("default_enabled", runtime.Http3.DisabledReason);
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

    public static void QuicListenerIdentityIsSeparateFromTcpIdentity()
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
        var listener = RuntimeListenerFor("http1AndHttp2AndHttp3Preview", experimentalHttp3: true);
        var protocols = listener.Protocols;
        var tcpAlpn = ListenerProtocolAdvertisement.BuildTcpAlpn(protocols);
        var quicAlpn = ListenerProtocolAdvertisement.BuildHttp3Alpn(listener);

        AssertEx.True(tcpAlpn.Contains(SslApplicationProtocol.Http2));
        AssertEx.True(tcpAlpn.Contains(SslApplicationProtocol.Http11));
        AssertEx.False(tcpAlpn.Any(static protocol => protocol.Protocol.Span.SequenceEqual("h3"u8)));
        AssertEx.True(quicAlpn.Any(static protocol => protocol.Protocol.Span.SequenceEqual("h3"u8)));
    }

    public static void StatusAndEffectiveProjectionReportLegacyHttp3PreviewEnabled()
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
        AssertEx.False(statusProjection.DefaultReadinessBlockers.Contains("qpack_dynamic_table_unsupported"));
        AssertEx.False(statusProjection.DefaultReadinessBlockers.Contains("request_body_buffered_not_streamed"));
        AssertEx.Equal("static_with_zero_dynamic_table", statusProjection.QpackMode);
        AssertEx.Equal("streaming", statusProjection.RequestBodyMode);
    }

    public static void FinalSupportProjectionReportsHttp3MatrixAndFinalNaming()
    {
        var options = ValidProxyOptions(Http3Listener("main", "http1AndHttp2", experimental: false));
        options.Routes[0].Upstreams[0] = new UpstreamOptions
        {
            Name = "h3",
            Scheme = "https",
            Protocol = "http3",
            Address = "upstream.test",
            Port = 443
        };
        var snapshot = ProxyConfigurationMapper.ToRuntimeSnapshot(
            options,
            new ProxyOperationalOptions(),
            new Dictionary<string, RuntimeCertificate>(StringComparer.OrdinalIgnoreCase),
            1,
            DateTimeOffset.UtcNow,
            "memory",
            [],
            Discovery());

        var projection = ProxyConfigurationMapper.ToProjection(snapshot).Http3;

        AssertEx.Equal("default_enabled_for_eligible_tls_proxy_listeners", projection.ClientHttp3SupportLevel);
        AssertEx.Equal("opt_in_https_quic_reused_multiplexed", projection.UpstreamHttp3SupportLevel);
        AssertEx.True(projection.ClientProtocols.SequenceEqual(["http1", "http2", "http3"]));
        AssertEx.True(projection.UpstreamProtocols.SequenceEqual(["http1", "http2", "http3"]));
        AssertEx.True(projection.SupportedRouteActions.Contains("proxy", StringComparer.Ordinal));
        AssertEx.True(projection.SupportedRouteActions.Contains("redirect", StringComparer.Ordinal));
        AssertEx.True(projection.SupportedRouteActions.Contains("staticResponse", StringComparer.Ordinal));
        AssertEx.True(projection.SupportedRouteActions.Contains("maintenance", StringComparer.Ordinal));
        AssertEx.True(projection.SupportedPolicyFeatures.Contains("cache_get_head", StringComparer.Ordinal));
        AssertEx.True(projection.SupportedPolicyFeatures.Contains("retry_circuit_safe_methods", StringComparer.Ordinal));
        AssertEx.True(projection.SupportedPolicyFeatures.Contains("weighted_balancing", StringComparer.Ordinal));
        AssertEx.True(projection.SupportedPolicyFeatures.Contains("health_checks", StringComparer.Ordinal));
        AssertEx.True(projection.UnsupportedFeatures.SequenceEqual(RuntimeHttp3UnsupportedFeatureCodes.EffectiveConfig));
        AssertEx.False(projection.UnsupportedFeatures.Contains("upstream_http3_multiplexing", StringComparer.Ordinal));
        AssertEx.Equal("reused_multiplexed", projection.UpstreamPoolingMode);
        AssertEx.True(projection.UpstreamMultiplexingEnabled);
        AssertEx.Equal(8, projection.UpstreamMaxStreamsPerConnection);
        AssertEx.Equal("", projection.UpstreamPoolingLimitationReason);
        if (QuicListener.IsSupported && QuicConnection.IsSupported)
        {
            AssertEx.Equal("default-enabled", projection.DefaultEnablementState);
            AssertEx.Equal("default_enabled_for_eligible_tls_proxy_listeners", projection.ReadinessConclusion);
        }
    }

    public static void UpstreamProtocolAcceptsExplicitHttp3()
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

        AssertEx.False(validation.Failed, string.Join("; ", validation.Failures ?? []));
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
        return RuntimeListenerProtocolExtensions.ParseConfigTextOrDefault(protocols);
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
