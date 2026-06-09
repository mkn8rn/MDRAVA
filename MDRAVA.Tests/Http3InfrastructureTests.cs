using System.Net.Security;
using System.Net.Quic;
using MDRAVA.API.Proxy.Configuration;
using MDRAVA.API.Proxy.Configuration.Loading;
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

    public static void ListenerProtocolConfigParsingUsesCurrentHttp3Spellings()
    {
        var cases = new (string Text, RuntimeListenerProtocols Protocols)[]
        {
            ("http1", RuntimeListenerProtocols.Http1),
            ("http2", RuntimeListenerProtocols.Http2),
            ("http1AndHttp2", RuntimeListenerProtocols.Http1AndHttp2),
            ("http3", RuntimeListenerProtocols.Http3),
            ("http1AndHttp3", RuntimeListenerProtocols.Http1AndHttp3),
            ("http2AndHttp3", RuntimeListenerProtocols.Http2AndHttp3),
            ("http1AndHttp2AndHttp3", RuntimeListenerProtocols.Http1AndHttp2AndHttp3)
        };

        foreach (var entry in cases)
        {
            var parsed = RuntimeListenerProtocolExtensions.TryParseConfigText(entry.Text, out var protocols);
            var compatibilityParsed = RuntimeHttp3Compatibility.TryParseProtocols(entry.Text, out var compatibilityProtocols);

            AssertEx.True(parsed, entry.Text);
            AssertEx.True(compatibilityParsed, entry.Text);
            AssertEx.Equal(entry.Protocols, protocols);
            AssertEx.Equal(entry.Protocols, compatibilityProtocols);
            AssertEx.Equal(entry.Text, protocols.ToConfigText());
        }

        AssertEx.True(RuntimeListenerProtocols.Http1AndHttp2AndHttp3.HasHttp3());
        AssertEx.Equal(
            RuntimeListenerProtocolExtensions.SupportedConfigValues.Count,
            cases.Length);
    }

    public static void Http3CompatibilityNormalizerCentralizesEnablementSemantics()
    {
        var enabled = RuntimeHttp3Compatibility.From(Http3Listener("stable", "http1AndHttp3"));
        var disabled = RuntimeHttp3Compatibility.From(new ListenerOptions
        {
            Name = "disabled",
            Address = "127.0.0.1",
            Port = 8443,
            Transport = "https",
            Protocols = "http3",
            Http3Enablement = "disabled",
            DefaultCertificateId = "default"
        });

        AssertEx.True(enabled.ProtocolsValid);
        AssertEx.True(enabled.EnablementValid);
        AssertEx.Equal(RuntimeHttp3Enablement.Default, enabled.EffectiveEnablement);
        AssertEx.True(enabled.ExplicitHttp3Requested);
        AssertEx.Equal(RuntimeHttp3Enablement.Disabled, disabled.EffectiveEnablement);
        AssertEx.True(disabled.EnablementExplicitlyConfigured);
    }

    public static void CurrentHttp3ConfigValidatesMapsAndAggregatesConsistently()
    {
        var listener = Http3Listener("main", "http1AndHttp2AndHttp3");
        var validation = new ProxyOptionsValidator().Validate(null, ValidProxyOptions(listener));
        var snapshot = ProxyConfigurationMapper.ToRuntimeSnapshot(
            ValidProxyOptions(listener),
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
                            listener
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
        AssertEx.Equal(RuntimeListenerProtocols.Http1AndHttp2AndHttp3, snapshot.Listeners[0].Protocols);
        AssertEx.Equal("default", snapshot.Listeners[0].Http3.EnablementLevel);
        AssertEx.True(snapshot.Listeners[0].Http3.EnabledForTraffic);
        AssertEx.False(aggregateValidation.Failed, string.Join("; ", aggregateValidation.Failures ?? []));
        AssertEx.Equal("http1AndHttp2AndHttp3", aggregated.Listeners[0].Protocols);
    }

    public static void LegacyHttp3ConfigAliasesAreRejected()
    {
        var options = ValidProxyOptions(Http3Listener("main", "http3"));
        options.Listeners.Add(new ListenerOptions
        {
            Name = "legacy",
            Address = "127.0.0.1",
            Port = 9443,
            Transport = "https",
            Protocols = "http3Preview",
            Http3Enablement = "preview",
            DefaultCertificateId = "default"
        });

        var validation = new ProxyOptionsValidator().Validate(null, options);
        var failures = AssertEx.NotNull(validation.Failures);

        AssertEx.True(validation.Failed);
        AssertEx.True(failures.Any(static failure => failure.Contains("Protocols must be", StringComparison.Ordinal)), string.Join("; ", failures));
        AssertEx.True(failures.Any(static failure => failure.Contains("Http3Enablement must be", StringComparison.Ordinal)), string.Join("; ", failures));
    }

    public static void RemovedHttp3ConfigPropertiesAreRejectedByParser()
    {
        var parser = new SiteConfigurationParser();
        var experimental = CaptureJsonException(() => parser.ReadSiteText(
            """
            {
              "name": "site",
              "listeners": [
                {
                  "name": "main",
                  "address": "127.0.0.1",
                  "port": 8443,
                  "transport": "https",
                  "protocols": "http3",
                  "experimentalHttp3": true,
                  "defaultCertificateId": "default"
                }
              ],
              "host": "localhost"
            }
            """,
            SiteConfigurationFormat.Json));
        var buffered = CaptureJsonException(() => parser.ReadSiteText(
            """
            {
              "name": "site",
              "listeners": [
                {
                  "name": "main",
                  "address": "127.0.0.1",
                  "port": 8443,
                  "transport": "https",
                  "protocols": "http3",
                  "http3MaxBufferedRequestBodyBytes": 4096,
                  "defaultCertificateId": "default"
                }
              ],
              "host": "localhost"
            }
            """,
            SiteConfigurationFormat.Json));

        AssertEx.True(experimental.Message.Contains("experimentalHttp3", StringComparison.Ordinal), experimental.Message);
        AssertEx.True(buffered.Message.Contains("http3MaxBufferedRequestBodyBytes", StringComparison.Ordinal), buffered.Message);
    }

    private static System.Text.Json.JsonException CaptureJsonException(Action action)
    {
        try
        {
            action();
        }
        catch (System.Text.Json.JsonException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Expected JSON exception.");
    }

    public static void Http3DefaultEnabledForEligibleTlsListener()
    {
        var listener = RuntimeListenerFor("http1");

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

    public static void Http3ProtocolUsesDefaultEnablement()
    {
        var validation = new ProxyOptionsValidator().Validate(
            null,
            ValidProxyOptions(Http3Listener("current", "http1AndHttp2AndHttp3")));
        var runtime = RuntimeListenerFor("http1AndHttp2AndHttp3");

        AssertEx.False(validation.Failed, string.Join("; ", validation.Failures ?? []));
        AssertEx.True(runtime.Http3.EnabledForTraffic);
        AssertEx.Equal("default", runtime.Http3.EnablementLevel);
        AssertEx.Equal("default_enabled", runtime.Http3.DisabledReason);
    }

    public static void Http3RequiresTlsCertificateCapableListener()
    {
        var listener = new ListenerOptions
        {
            Name = "current",
            Address = "127.0.0.1",
            Port = 8443,
            Transport = "https",
            Protocols = "http1AndHttp2AndHttp3",
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

    public static void Http3ConfigEnablesTraffic()
    {
        var listener = Http3Listener("current", "http1AndHttp2AndHttp3");
        var validation = new ProxyOptionsValidator().Validate(null, ValidProxyOptions(listener));
        var runtime = RuntimeListenerFor("http1AndHttp2AndHttp3");

        AssertEx.False(validation.Failed, string.Join("; ", validation.Failures ?? []));
        AssertEx.True(runtime.TcpTrafficEnabled);
        AssertEx.True(runtime.Http3.EnabledForTraffic);
        AssertEx.Equal("default_enabled", runtime.Http3.DisabledReason);
    }

    public static void Http3OnlyDoesNotEnableTcpTraffic()
    {
        var listener = Http3Listener("current", "http3");
        var validation = new ProxyOptionsValidator().Validate(null, ValidProxyOptions(listener));
        var runtime = RuntimeListenerFor("http3");

        AssertEx.False(validation.Failed, string.Join("; ", validation.Failures ?? []));
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
        var listener = RuntimeListenerFor("http1AndHttp2AndHttp3");
        var tcpIdentity = listener.Identity;
        var quicIdentity = AssertEx.NotNull(listener.QuicIdentity);

        AssertEx.Equal("main|quic", quicIdentity.Key);
        AssertEx.Equal("127.0.0.1|8443|udp|quic", quicIdentity.BindKey);
        AssertEx.False(string.Equals(tcpIdentity.BindKey, quicIdentity.BindKey, StringComparison.Ordinal));
    }

    public static void TcpAlpnDoesNotAdvertiseHttp3()
    {
        var listener = RuntimeListenerFor("http1AndHttp2AndHttp3");
        var protocols = listener.Protocols;
        var tcpAlpn = ListenerProtocolAdvertisement.BuildTcpAlpn(protocols);
        var quicAlpn = ListenerProtocolAdvertisement.BuildHttp3Alpn(listener);

        AssertEx.True(tcpAlpn.Contains(SslApplicationProtocol.Http2));
        AssertEx.True(tcpAlpn.Contains(SslApplicationProtocol.Http11));
        AssertEx.False(tcpAlpn.Any(static protocol => protocol.Protocol.Span.SequenceEqual("h3"u8)));
        AssertEx.True(quicAlpn.Any(static protocol => protocol.Protocol.Span.SequenceEqual("h3"u8)));
    }

    public static void StatusAndEffectiveProjectionReportHttp3Enabled()
    {
        var snapshot = ProxyConfigurationMapper.ToRuntimeSnapshot(
            ValidProxyOptions(Http3Listener("current", "http1AndHttp2AndHttp3")),
            new ProxyOperationalOptions(),
            new Dictionary<string, RuntimeCertificate>(StringComparer.OrdinalIgnoreCase),
            1,
            DateTimeOffset.UtcNow,
            "memory",
            [],
            Discovery());
        var projection = ProxyConfigurationProjectionMapper.ToProjection(snapshot);

        AssertEx.Equal("default", projection.Http3.Configured);
        AssertEx.True(projection.Http3.EnabledForTraffic);
        AssertEx.Equal("default_enabled", projection.Http3.DisabledReason);
        AssertEx.True(projection.Http3.UdpQuicListenerIdentityModeled);
        var statusProjection = Http3RuntimeSupport.Project(snapshot.Listeners);
        AssertEx.Equal("default", statusProjection.Configured);
        AssertEx.True(statusProjection.EnabledForTraffic);
        AssertEx.Equal("default_enabled", statusProjection.DisabledReason);
        AssertEx.False(statusProjection.DefaultReadinessBlockers.Contains("qpack_dynamic_table_unsupported"));
        AssertEx.False(statusProjection.DefaultReadinessBlockers.Contains("request_body_buffered_not_streamed"));
        AssertEx.Equal("static_with_zero_dynamic_table", statusProjection.QpackMode);
        AssertEx.Equal("streaming", statusProjection.RequestBodyMode);
    }

    public static void FinalSupportProjectionReportsHttp3MatrixAndFinalNaming()
    {
        var options = ValidProxyOptions(Http3Listener("main", "http1AndHttp2"));
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

        var projection = ProxyConfigurationProjectionMapper.ToProjection(snapshot).Http3;

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

    private static ListenerOptions Http3Listener(string name, string protocols)
    {
        return new ListenerOptions
        {
            Name = name,
            Address = "127.0.0.1",
            Port = 8443,
            Transport = "https",
            Protocols = protocols,
            DefaultCertificateId = "default"
        };
    }

    private static RuntimeListener RuntimeListenerFor(string protocols)
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
            Protocols = ParseProtocols(protocols)
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
