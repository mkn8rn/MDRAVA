#pragma warning disable CA1416
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using MDRAVA.API.Controllers;
using MDRAVA.API.Models.Diagnostics;
using MDRAVA.API.Proxy.Configuration.Loading;
using MDRAVA.API.Proxy.Configuration.Paths;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Configuration.Storage;
using MDRAVA.API.Proxy.Configuration;
using MDRAVA.API.Proxy.Diagnostics;
using MDRAVA.API.Proxy.Health;
using MDRAVA.API.Proxy.Hosting;
using MDRAVA.API.Proxy.Http3;
using MDRAVA.API.Proxy.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MDRAVA.Tests;

internal static class ClientHttp3Tests
{
    private static readonly SslApplicationProtocol Http3Alpn = new("h3");

    public static void Http3DefaultEnabledForEligibleTlsListener()
    {
        var listener = new RuntimeListener(
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
            64 * 1024);

        AssertEx.False(listener.Http3PreviewConfigured);
        AssertEx.True(listener.Http3.Configured);
        AssertEx.True(listener.Http3.EnabledForTraffic);
        AssertEx.Equal("default", listener.Http3.EnablementLevel);
        AssertEx.Equal("default_enabled", listener.Http3.DisabledReason);
    }

    public static void ExplicitHttp3DisablePreventsTraffic()
    {
        var validation = new ProxyOptionsValidator().Validate(
            null,
            new ProxyOptions
            {
                Listeners =
                [
                    new ListenerOptions
                    {
                        Name = "main",
                        Address = "127.0.0.1",
                        Port = 8443,
                        Transport = "https",
                        Protocols = "http1AndHttp2",
                        Http3Enablement = "disabled",
                        DefaultCertificateId = "default"
                    }
                ],
                Routes =
                [
                    new ProxyRouteOptions
                    {
                        Name = "static",
                        Host = "*",
                        PathPrefix = "/",
                        Action = "staticResponse"
                    }
                ]
            });
        var listener = new RuntimeListener(
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
            Http3Enablement = RuntimeHttp3Enablement.Disabled
        };

        AssertEx.False(validation.Failed, string.Join("; ", validation.Failures ?? []));
        AssertEx.False(listener.Http3.Configured);
        AssertEx.False(listener.Http3.EnabledForTraffic);
        AssertEx.Equal("disabled", listener.Http3.DisabledReason);
    }

    public static void QuicListenerIdentityIsSeparateFromTcpIdentity()
    {
        var listener = LegacyHttp3Listener("http1AndHttp2AndHttp3Preview", experimental: true);
        var tcp = listener.Identity;
        var quic = AssertEx.NotNull(listener.QuicIdentity);

        AssertEx.Equal("main", tcp.Key);
        AssertEx.Equal("main|quic", quic.Key);
        AssertEx.False(string.Equals(tcp.BindKey, quic.BindKey, StringComparison.Ordinal));
    }

    public static async Task FailedQuicListenerStartDoesNotBreakTcpListener()
    {
        using var temp = TemporaryDirectory.Create();
        var port = GetFreeTcpUdpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3Site(temp.Path, port, "http1AndHttp3Preview", staticBody: "unused");
        using var host = BuildProxyHost(
            temp.Path,
            services => services.AddSingleton<IHttp3QuicListenerFactory, FailingQuicListenerFactory>());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await host.StartAsync(timeout.Token);
        try
        {
            var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
            await WaitForListenerAsync(runtime, "main", "tcp", ProxyListenerState.Active, timeout.Token);
            await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Failed, timeout.Token);
            var snapshot = runtime.Snapshot();

            AssertEx.True(snapshot.IsRunning);
            AssertEx.True(snapshot.Listeners.Any(static listener => listener.Kind == "tcp" && listener.State == ProxyListenerState.Active));
            AssertEx.True(snapshot.Listeners.Any(static listener => listener.Kind == "quic" && listener.State == ProxyListenerState.Failed));
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    public static async Task DefaultHttp3TlsListenerStartsQuicAndEmitsAltSvc()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var temp = TemporaryDirectory.Create();
        var port = GetFreeTcpUdpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3Site(
            temp.Path,
            port,
            "http1AndHttp2",
            staticBody: "default-h3",
            experimental: false,
            altSvcMaxAgeSeconds: 60,
            http3EnablementOverride: "default");
        using var host = BuildProxyHost(temp.Path);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await host.StartAsync(timeout.Token);
        try
        {
            var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
            await WaitForListenerAsync(runtime, "main", "tcp", ProxyListenerState.Active, timeout.Token);
            await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);
            var response = await SendHttp1TlsRequestAsync(port, "/alt", timeout.Token);
            var status = new ProxyStatusController(
                runtime,
                host.Services.GetRequiredService<ProxyMetrics>(),
                host.Services.GetRequiredService<IProxyConfigurationStore>(),
                host.Services.GetRequiredService<UpstreamHealthStore>())
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            }.Get();

            AssertEx.True(response.Contains($"Alt-Svc: h3=\":{port}\"; ma=60", StringComparison.Ordinal), response);
            AssertEx.Equal("default", status.Http3.Configured);
            AssertEx.True(status.Http3.EnabledForTraffic);
            AssertEx.True(status.Http3.AltSvcActive);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    public static async Task SuccessfulReloadCanAddAndRemoveQuicListener()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var temp = TemporaryDirectory.Create();
        var port = GetFreeTcpUdpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3Site(temp.Path, port, "http1", staticBody: "unused", experimental: false);
        using var host = BuildProxyHost(temp.Path);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await host.StartAsync(timeout.Token);
        try
        {
            var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
            await WaitForListenerAsync(runtime, "main", "tcp", ProxyListenerState.Active, timeout.Token);

            WriteHttp3Site(temp.Path, port, "http1AndHttp3Preview", staticBody: "unused");
            var add = await host.Services.GetRequiredService<IProxyConfigurationReloadService>().ReloadAsync(timeout.Token);
            await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);

            WriteHttp3Site(temp.Path, port, "http1", staticBody: "unused", experimental: false);
            var remove = await host.Services.GetRequiredService<IProxyConfigurationReloadService>().ReloadAsync(timeout.Token);
            await WaitForNoListenerAsync(runtime, "main", "quic", timeout.Token);

            AssertEx.True(add.Succeeded, string.Join("; ", add.Errors));
            AssertEx.True(remove.Succeeded, string.Join("; ", remove.Errors));
            AssertEx.True(AssertEx.NotNull(add.ListenerReload).Added >= 1);
            AssertEx.True(AssertEx.NotNull(remove.ListenerReload).Removed >= 1);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    public static async Task FailedReloadPreservesOldQuicListenerSet()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var temp = TemporaryDirectory.Create();
        var port = GetFreeTcpUdpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3Site(temp.Path, port, "http1AndHttp3Preview", staticBody: "live");
        using var host = BuildProxyHost(temp.Path);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await host.StartAsync(timeout.Token);
        try
        {
            var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
            var before = await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);
            File.WriteAllText(Path.Combine(temp.Path, "config", "sites", "broken.json"), "{ nope");

            var reload = await host.Services.GetRequiredService<IProxyConfigurationReloadService>().ReloadAsync(timeout.Token);
            var after = await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);

            AssertEx.False(reload.Succeeded);
            AssertEx.Equal(before.StartedAtUtc, after.StartedAtUtc);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    public static async Task SuccessfulHttp3CertificateReloadKeepsQuicListenerAndUsesNewCertificate()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var temp = TemporaryDirectory.Create();
        var port = GetFreeTcpUdpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3Site(temp.Path, port, "http1AndHttp3Preview", staticBody: "cert-live");
        using var host = BuildProxyHost(temp.Path);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await host.StartAsync(timeout.Token);
        try
        {
            var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
            var before = await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);
            var beforeSubject = "";
            await using var activeConnection = await ConnectHttp3Async(port, timeout.Token, subject => beforeSubject = subject);
            await using (var beforeStream = await activeConnection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, timeout.Token))
            {
                await WriteHttp3RequestAsync(beforeStream, "GET", "/before-cert", null, timeout.Token);
                var beforeResponse = DecodeHttp3Response(await ReadToEndAsync(beforeStream, timeout.Token));
                AssertEx.Equal("200", HeaderValue(beforeResponse.Headers, ":status"));
                AssertEx.Equal("cert-live", beforeResponse.Body);
            }

            TestCertificates.WriteSelfSignedPfx(Path.Combine(temp.Path, "certs", "home.pfx"), "localhost-reloaded", "secret");
            var reload = await host.Services.GetRequiredService<IProxyConfigurationReloadService>().ReloadAsync(timeout.Token);
            var after = await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);
            await using (var activeAfterStream = await activeConnection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, timeout.Token))
            {
                await WriteHttp3RequestAsync(activeAfterStream, "GET", "/after-cert-active", null, timeout.Token);
                var activeAfterResponse = DecodeHttp3Response(await ReadToEndAsync(activeAfterStream, timeout.Token));
                AssertEx.Equal("200", HeaderValue(activeAfterResponse.Headers, ":status"));
                AssertEx.Equal("cert-live", activeAfterResponse.Body);
            }

            var afterSubject = "";
            var afterResponse = await SendHttp3RequestAsync(
                port,
                "GET",
                "/after-cert",
                timeout.Token,
                certificateSubjectObserver: subject => afterSubject = subject);

            AssertEx.True(reload.Succeeded, string.Join("; ", reload.Errors));
            AssertEx.Equal("200", HeaderValue(afterResponse.Headers, ":status"));
            AssertEx.Equal("cert-live", afterResponse.Body);
            AssertEx.True(beforeSubject.Contains("CN=localhost", StringComparison.Ordinal), beforeSubject);
            AssertEx.True(afterSubject.Contains("CN=localhost-reloaded", StringComparison.Ordinal), afterSubject);
            AssertEx.Equal(before.StartedAtUtc, after.StartedAtUtc);
            await activeConnection.CloseAsync(0, CancellationToken.None);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    public static async Task FailedHttp3CertificateReloadPreservesPreviousQuicCertificate()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var temp = TemporaryDirectory.Create();
        var port = GetFreeTcpUdpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3Site(temp.Path, port, "http1AndHttp3Preview", staticBody: "cert-live");
        using var host = BuildProxyHost(temp.Path);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await host.StartAsync(timeout.Token);
        try
        {
            var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
            var before = await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);
            var beforeSubject = "";
            var beforeResponse = await SendHttp3RequestAsync(
                port,
                "GET",
                "/before-failed-cert",
                timeout.Token,
                certificateSubjectObserver: subject => beforeSubject = subject);
            TestCertificates.WriteSelfSignedPfx(Path.Combine(temp.Path, "certs", "home.pfx"), "localhost-reloaded", "secret");
            File.WriteAllText(Path.Combine(temp.Path, "config", "sites", "broken.json"), "{ nope");

            var reload = await host.Services.GetRequiredService<IProxyConfigurationReloadService>().ReloadAsync(timeout.Token);
            var after = await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);
            var afterSubject = "";
            var afterResponse = await SendHttp3RequestAsync(
                port,
                "GET",
                "/after-failed-cert",
                timeout.Token,
                certificateSubjectObserver: subject => afterSubject = subject);

            AssertEx.False(reload.Succeeded);
            AssertEx.Equal("200", HeaderValue(beforeResponse.Headers, ":status"));
            AssertEx.Equal("200", HeaderValue(afterResponse.Headers, ":status"));
            AssertEx.True(beforeSubject.Contains("CN=localhost", StringComparison.Ordinal), beforeSubject);
            AssertEx.True(afterSubject.Contains("CN=localhost", StringComparison.Ordinal), afterSubject);
            AssertEx.Equal(before.StartedAtUtc, after.StartedAtUtc);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    public static void StatusAndEffectiveConfigPreserveLegacyHttp3PreviewProjection()
    {
        var snapshot = ProxyConfigurationMapper.ToRuntimeSnapshot(
            new ProxyOptions
            {
                Listeners =
                [
                    new ListenerOptions
                    {
                        Name = "main",
                        Address = "127.0.0.1",
                        Port = 8443,
                        Transport = "https",
                        Protocols = "http3Preview",
                        ExperimentalHttp3 = true,
                        DefaultCertificateId = "default"
                    }
                ],
                Routes =
                [
                    new ProxyRouteOptions
                    {
                        Name = "static",
                        Host = "*",
                        PathPrefix = "/",
                        Action = "staticResponse"
                    }
                ]
            },
            new ProxyOperationalOptions(),
            new Dictionary<string, RuntimeCertificate>(StringComparer.OrdinalIgnoreCase),
            1,
            DateTimeOffset.UtcNow,
            "memory",
            [],
            new ProxyConfigurationDiscovery(
                new ProxyFilesystemLayout("data", "config", "sites", "logs", "certs", "state", "proxy.json"),
                [],
                [],
                []));

        var projection = ProxyConfigurationMapper.ToProjection(snapshot);

        AssertEx.Equal("preview", projection.Http3.Configured);
        AssertEx.True(projection.Http3.EnabledForTraffic);
        AssertEx.Equal("preview_enabled", projection.Http3.DisabledReason);
        AssertEx.False(projection.Http3.DefaultReadinessBlockers.Contains("qpack_dynamic_table_unsupported"));
        AssertEx.False(projection.Http3.DefaultReadinessBlockers.Contains("request_body_buffered_not_streamed"));
        AssertEx.Equal("static_with_zero_dynamic_table", projection.Http3.QpackMode);
        AssertEx.Equal(0, projection.Http3.QpackDynamicTableCapacity);
        AssertEx.Equal(0, projection.Http3.QpackBlockedStreams);
        AssertEx.Equal("streaming", projection.Http3.RequestBodyMode);
        AssertEx.True(snapshot.Listeners[0].Http3.ExperimentalGateEnabled);
    }

    public static void Http3BetaEnablementIsExplicitlyProjected()
    {
        var snapshot = ProxyConfigurationMapper.ToRuntimeSnapshot(
            new ProxyOptions
            {
                Listeners =
                [
                    new ListenerOptions
                    {
                        Name = "main",
                        Address = "127.0.0.1",
                        Port = 8443,
                        Transport = "https",
                        Protocols = "http3Preview",
                        ExperimentalHttp3 = true,
                        Http3Enablement = "beta",
                        DefaultCertificateId = "default"
                    }
                ],
                Routes =
                [
                    new ProxyRouteOptions
                    {
                        Name = "static",
                        Host = "*",
                        PathPrefix = "/",
                        Action = "staticResponse"
                    }
                ]
            },
            new ProxyOperationalOptions(),
            new Dictionary<string, RuntimeCertificate>(StringComparer.OrdinalIgnoreCase),
            1,
            DateTimeOffset.UtcNow,
            "memory",
            [],
            new ProxyConfigurationDiscovery(
                new ProxyFilesystemLayout("data", "config", "sites", "logs", "certs", "state", "proxy.json"),
                [],
                [],
                []));

        var projection = ProxyConfigurationMapper.ToProjection(snapshot);

        AssertEx.Equal("beta", projection.Http3.EnablementLevel);
        AssertEx.Equal("beta", snapshot.Listeners[0].Http3.EnablementLevel);
        AssertEx.Equal("beta_enabled", snapshot.Listeners[0].Http3.DisabledReason);
    }

    public static async Task AltSvcIsAbsentWhenHttp3ExplicitlyDisabled()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var temp = TemporaryDirectory.Create();
        var port = GetFreeTcpUdpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3Site(temp.Path, port, "http1", staticBody: "alt-disabled", experimental: false);
        using var host = BuildProxyHost(temp.Path);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await host.StartAsync(timeout.Token);
        try
        {
            var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
            await WaitForListenerAsync(runtime, "main", "tcp", ProxyListenerState.Active, timeout.Token);
            var response = await SendHttp1TlsRequestAsync(port, "/alt", timeout.Token);

            AssertEx.False(response.Contains("Alt-Svc:", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    public static async Task AltSvcIsEmittedOnlyWhenConfiguredAndReady()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var temp = TemporaryDirectory.Create();
        var port = GetFreeTcpUdpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3Site(
            temp.Path,
            port,
            "http1AndHttp3Preview",
            staticBody: "alt-ready",
            altSvcEnabled: true,
            altSvcMaxAgeSeconds: 60);
        using var host = BuildProxyHost(temp.Path);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await host.StartAsync(timeout.Token);
        try
        {
            var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
            await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);
            var response = await SendHttp1TlsRequestAsync(port, "/alt", timeout.Token);
            var status = new ProxyStatusController(
                runtime,
                host.Services.GetRequiredService<ProxyMetrics>(),
                host.Services.GetRequiredService<IProxyConfigurationStore>(),
                host.Services.GetRequiredService<UpstreamHealthStore>())
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            }.Get();

            AssertEx.True(response.Contains($"Alt-Svc: h3=\":{port}\"; ma=60", StringComparison.Ordinal), response);
            AssertEx.True(status.Http3.QuicListenerReady);
            AssertEx.True(status.Http3.AltSvcActive);
            AssertEx.Equal("default_enabled_for_eligible_tls_proxy_listeners", status.Http3.ReadinessConclusion);
            AssertEx.Equal("active", status.Http3.AltSvcStateReason);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    public static async Task AltSvcIsNotEmittedWhenQuicListenerIsNotReady()
    {
        using var temp = TemporaryDirectory.Create();
        var port = GetFreeTcpUdpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3Site(
            temp.Path,
            port,
            "http1AndHttp3Preview",
            staticBody: "alt-failed",
            altSvcEnabled: true);
        using var host = BuildProxyHost(
            temp.Path,
            services => services.AddSingleton<IHttp3QuicListenerFactory, FailingQuicListenerFactory>());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await host.StartAsync(timeout.Token);
        try
        {
            var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
            await WaitForListenerAsync(runtime, "main", "tcp", ProxyListenerState.Active, timeout.Token);
            await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Failed, timeout.Token);
            var response = await SendHttp1TlsRequestAsync(port, "/alt", timeout.Token);

            AssertEx.False(response.Contains("Alt-Svc:", StringComparison.OrdinalIgnoreCase), response);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    public static void AdminResponsesDoNotEmitAltSvc()
    {
        using var temp = TemporaryDirectory.Create();
        WriteCertificateConfig(temp.Path);
        using var host = BuildProxyHost(temp.Path);
        var controller = new ProxyStatusController(
            host.Services.GetRequiredService<ProxyRuntimeState>(),
            host.Services.GetRequiredService<ProxyMetrics>(),
            host.Services.GetRequiredService<IProxyConfigurationStore>(),
            host.Services.GetRequiredService<UpstreamHealthStore>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        _ = controller.Get();

        AssertEx.False(controller.Response.Headers.ContainsKey("Alt-Svc"));
    }

    public static async Task MinimalHttp3GetCanReachGeneratedRoute()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var result = await RunHttp3GeneratedRouteScenarioAsync("GET", "/hello?x=1", "hello-h3");

        AssertEx.Equal("200", HeaderValue(result.Headers, ":status"));
        AssertEx.Equal("hello-h3", result.Body);
        AssertEx.True(result.Metrics.Http3AcceptedConnections >= 1);
        AssertEx.True(result.Metrics.Http3Requests >= 1);
    }

    public static async Task HeadReturnsHeadersWithoutBody()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var result = await RunHttp3GeneratedRouteScenarioAsync("HEAD", "/head", "head-body");

        AssertEx.Equal("200", HeaderValue(result.Headers, ":status"));
        AssertEx.Equal("9", HeaderValue(result.Headers, "content-length"));
        AssertEx.Equal("", result.Body);
    }

    public static async Task Http3GeneratedRedirectRouteWorks()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var result = await RunHttp3GeneratedRouteScenarioAsync(
            "GET",
            "/old?id=1",
            "unused",
            routeJson:
            """
                {
                  "name": "redirect",
                  "pathPrefix": "/old",
                  "action": "redirect",
                  "redirect": {
                    "statusCode": 308,
                    "targetPath": "/new",
                    "preserveQuery": true
                  }
                }
            """);

        AssertEx.Equal("308", HeaderValue(result.Headers, ":status"));
        AssertEx.Equal("/new?id=1", HeaderValue(result.Headers, "location"));
        AssertEx.Equal("", result.Body);
    }

    public static async Task Http3GeneratedMaintenanceRouteWorks()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var result = await RunHttp3GeneratedRouteScenarioAsync(
            "GET",
            "/maintenance",
            "unused",
            routeJson:
            """
                {
                  "name": "maintenance",
                  "pathPrefix": "/maintenance",
                  "action": "proxy",
                  "maintenance": {
                    "enabled": true,
                    "retryAfterSeconds": 120,
                    "body": "maintenance-h3"
                  },
                  "upstreams": [
                    {
                      "name": "unused",
                      "address": "127.0.0.1",
                      "port": 65535
                    }
                  ]
                }
            """);

        AssertEx.Equal("503", HeaderValue(result.Headers, ":status"));
        AssertEx.Equal("120", HeaderValue(result.Headers, "retry-after"));
        AssertEx.Equal("maintenance-h3", result.Body);
    }

    public static async Task Http3RouteMissReturnsSafe404()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var result = await RunHttp3GeneratedRouteScenarioAsync(
            "GET",
            "/missing",
            "unused",
            routeJson:
            """
                {
                  "name": "known",
                  "pathPrefix": "/known",
                  "action": "staticResponse",
                  "staticResponse": {
                    "statusCode": 200,
                    "contentType": "text/plain",
                    "body": "known"
                  }
                }
            """);

        AssertEx.Equal("404", HeaderValue(result.Headers, ":status"));
        AssertEx.Equal("Not Found", result.Body);
    }

    public static async Task Http3RouteMissRemainsStableAcrossRepeatedReadyListenerRequests()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var temp = TemporaryDirectory.Create();
        var port = GetFreeTcpUdpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3Site(
            temp.Path,
            port,
            "http3Preview",
            "unused",
            routeJson:
            """
                {
                  "name": "known",
                  "pathPrefix": "/known",
                  "action": "staticResponse",
                  "staticResponse": {
                    "statusCode": 200,
                    "contentType": "text/plain",
                    "body": "known"
                  }
                }
            """);
        using var host = BuildProxyHost(temp.Path);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StartAsync(timeout.Token);

        try
        {
            var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
            await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);
            for (var index = 0; index < 3; index++)
            {
                var response = await SendHttp3RequestAsync(port, "GET", $"/missing-{index}", timeout.Token);
                AssertEx.Equal("404", HeaderValue(response.Headers, ":status"));
                AssertEx.Equal("Not Found", response.Body);
            }
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    public static async Task Http3GetProxyRouteWorks()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var result = await RunHttp3ProxyRouteScenarioAsync(
            "GET",
            "/proxy",
            "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 9\r\n\r\nh3-proxy");

        AssertEx.Equal("200", HeaderValue(result.Headers, ":status"));
        AssertEx.Equal("h3-proxy", result.Body);
        AssertEx.True(result.UpstreamRequest.StartsWith("GET /proxy HTTP/1.1", StringComparison.Ordinal), result.UpstreamRequest);
        AssertEx.True(result.Metrics.Http3ProxiedRequests >= 1);
    }

    public static async Task Http3HeadProxyRouteWorks()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var result = await RunHttp3ProxyRouteScenarioAsync(
            "HEAD",
            "/head",
            "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 9\r\nX-Head: yes\r\n\r\nh3-proxy");

        AssertEx.Equal("200", HeaderValue(result.Headers, ":status"));
        AssertEx.Equal("yes", HeaderValue(result.Headers, "x-head"));
        AssertEx.Equal("", result.Body);
        AssertEx.True(result.UpstreamRequest.StartsWith("HEAD /head HTTP/1.1", StringComparison.Ordinal), result.UpstreamRequest);
    }

    public static async Task Http3ProxyPreservesQueryString()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var result = await RunHttp3ProxyRouteScenarioAsync(
            "GET",
            "/search?q=one&sort=two",
            "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 2\r\n\r\nok");

        AssertEx.True(
            result.UpstreamRequest.StartsWith("GET /search?q=one&sort=two HTTP/1.1", StringComparison.Ordinal),
            result.UpstreamRequest);
    }

    public static async Task Http3ProxyStripsPseudoHeadersBeforeUpstream()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var result = await RunHttp3ProxyRouteScenarioAsync(
            "GET",
            "/headers",
            "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 2\r\n\r\nok");

        AssertEx.False(result.UpstreamRequest.Contains(":method", StringComparison.OrdinalIgnoreCase));
        AssertEx.False(result.UpstreamRequest.Contains(":scheme", StringComparison.OrdinalIgnoreCase));
        AssertEx.False(result.UpstreamRequest.Contains(":authority", StringComparison.OrdinalIgnoreCase));
        AssertEx.False(result.UpstreamRequest.Contains(":path", StringComparison.OrdinalIgnoreCase));
    }

    public static async Task Http3ResponseHeadersAreEncodedSafely()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var result = await RunHttp3ProxyRouteScenarioAsync(
            "GET",
            "/safe-headers",
            "HTTP/1.1 200 OK\r\nConnection: close\r\nKeep-Alive: timeout=5\r\nContent-Length: 2\r\nX-Safe: yes\r\n\r\nok");

        AssertEx.Equal("200", HeaderValue(result.Headers, ":status"));
        AssertEx.Equal("yes", HeaderValue(result.Headers, "x-safe"));
        AssertEx.False(HeaderExists(result.Headers, "connection"));
        AssertEx.False(HeaderExists(result.Headers, "keep-alive"));
    }

    public static async Task Http3ChunkedResponseStreamsBodyWithoutTransferEncoding()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var result = await RunHttp3ProxyRouteScenarioAsync(
            "GET",
            "/chunked",
            "HTTP/1.1 200 OK\r\nConnection: close\r\nTransfer-Encoding: chunked\r\nX-Mode: chunked\r\n\r\n4\r\nwiki\r\n5\r\npedia\r\n0\r\nX-Trailer: ignored\r\n\r\n");

        AssertEx.Equal("200", HeaderValue(result.Headers, ":status"));
        AssertEx.Equal("chunked", HeaderValue(result.Headers, "x-mode"));
        AssertEx.Equal("wikipedia", result.Body);
        AssertEx.False(HeaderExists(result.Headers, "transfer-encoding"));
        AssertEx.True(result.Metrics.Http3StreamedResponses >= 1);
        AssertEx.True(result.Metrics.Http3ResponseBytesSent >= "wikipedia".Length);
    }

    public static async Task Http3ResponseStreamsBeforeUpstreamCompletes()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        var temp = TemporaryDirectory.Create();
        var proxyPort = GetFreeTcpUdpPort();
        var upstreamPort = GetFreeTcpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3ProxySite(temp.Path, proxyPort, upstreamPort);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var firstChunkSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseUpstream = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var upstreamTask = RunStreamingResponseUpstreamAsync(upstreamPort, firstChunkSent, releaseUpstream, timeout.Token);
        var host = BuildProxyHost(temp.Path);
        await host.StartAsync(timeout.Token);

        try
        {
            var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
            await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);
            await using var connection = await ConnectHttp3Async(proxyPort, timeout.Token);
            await using var stream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, timeout.Token);
            await WriteHttp3RequestAsync(stream, "GET", "/stream", null, timeout.Token);
            await firstChunkSent.Task.WaitAsync(timeout.Token);

            var firstData = await ReadFirstHttp3DataAsync(stream, timeout.Token);
            releaseUpstream.SetResult();
            _ = await ReadHttp3ResponseRemainderAsync(stream, timeout.Token);
            var upstreamRequest = await upstreamTask.WaitAsync(timeout.Token);
            var metrics = host.Services.GetRequiredService<ProxyMetrics>().Snapshot();

            AssertEx.Equal("stream-", firstData);
            AssertEx.True(upstreamRequest.StartsWith("GET /stream HTTP/1.1", StringComparison.Ordinal), upstreamRequest);
            AssertEx.Equal(0L, metrics.ActiveHttp3ResponseStreams);
        }
        finally
        {
            releaseUpstream.TrySetResult();
            await host.StopAsync(CancellationToken.None);
            host.Dispose();
            temp.Dispose();
        }
    }

    public static async Task Http3CacheInteractionUsesStoredResponse()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        var temp = TemporaryDirectory.Create();
        var proxyPort = GetFreeTcpUdpPort();
        var upstreamPort = GetFreeTcpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3ProxySite(
            temp.Path,
            proxyPort,
            upstreamPort,
            """
                  "cache": {
                    "enabled": true,
                    "maxEntryBytes": 4096,
                    "maxTotalBytes": 8192,
                    "defaultTtlSeconds": 60,
                    "respectOriginCacheControl": false
                  },
            """);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var upstreamTask = RunSingleResponseUpstreamAsync(
            upstreamPort,
            "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 6\r\n\r\ncached",
            timeout.Token);
        var host = BuildProxyHost(temp.Path);
        await host.StartAsync(timeout.Token);

        try
        {
            var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
            await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);
            var first = await SendHttp3RequestAsync(proxyPort, "GET", "/cache?x=1", timeout.Token);
            var upstreamRequest = await upstreamTask.WaitAsync(timeout.Token);
            var second = await SendHttp3RequestAsync(proxyPort, "GET", "/cache?x=1", timeout.Token);
            var metrics = host.Services.GetRequiredService<ProxyMetrics>().Snapshot();

            AssertEx.Equal("cached", first.Body);
            AssertEx.Equal("cached", second.Body);
            AssertEx.True(upstreamRequest.StartsWith("GET /cache?x=1 HTTP/1.1", StringComparison.Ordinal), upstreamRequest);
            AssertEx.True(metrics.Http3ProxiedRequests >= 1);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
            host.Dispose();
            temp.Dispose();
        }
    }

    public static async Task Http3OversizedCacheCandidateStreamsButIsNotCached()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        var temp = TemporaryDirectory.Create();
        var proxyPort = GetFreeTcpUdpPort();
        var upstreamPort = GetFreeTcpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3ProxySite(
            temp.Path,
            proxyPort,
            upstreamPort,
            """
                  "cache": {
                    "enabled": true,
                    "maxEntryBytes": 4,
                    "maxTotalBytes": 8192,
                    "defaultTtlSeconds": 60,
                    "respectOriginCacheControl": false
                  },
            """);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var upstreamTask = RunSequentialResponseUpstreamAsync(
            upstreamPort,
            [
                "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 11\r\n\r\nfirst-large",
                "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 10\r\n\r\nsecond-big"
            ],
            timeout.Token);
        var host = BuildProxyHost(temp.Path);
        await host.StartAsync(timeout.Token);

        try
        {
            var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
            await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);
            var first = await SendHttp3RequestAsync(proxyPort, "GET", "/large", timeout.Token);
            var second = await SendHttp3RequestAsync(proxyPort, "GET", "/large", timeout.Token);
            var upstreamRequests = await upstreamTask.WaitAsync(timeout.Token);

            AssertEx.Equal("first-large", first.Body);
            AssertEx.Equal("second-big", second.Body);
            AssertEx.Equal(2, upstreamRequests.Length);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
            host.Dispose();
            temp.Dispose();
        }
    }

    public static async Task Http3RetryForGetCanReachSecondUpstream()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        var temp = TemporaryDirectory.Create();
        var proxyPort = GetFreeTcpUdpPort();
        var firstUpstreamPort = GetFreeTcpPort();
        var secondUpstreamPort = GetFreeTcpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3RetrySite(temp.Path, proxyPort, firstUpstreamPort, secondUpstreamPort);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var upstreamTask = RunSingleResponseUpstreamAsync(
            secondUpstreamPort,
            "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 5\r\n\r\nretry",
            timeout.Token);
        var host = BuildProxyHost(temp.Path);
        await host.StartAsync(timeout.Token);

        try
        {
            var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
            await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);
            var response = await SendHttp3RequestAsync(proxyPort, "GET", "/retry", timeout.Token);
            var upstreamRequest = await upstreamTask.WaitAsync(timeout.Token);
            var metrics = host.Services.GetRequiredService<ProxyMetrics>().Snapshot();

            AssertEx.Equal("200", HeaderValue(response.Headers, ":status"));
            AssertEx.Equal("retry", response.Body);
            AssertEx.True(upstreamRequest.StartsWith("GET /retry HTTP/1.1", StringComparison.Ordinal), upstreamRequest);
            AssertEx.True(metrics.RetryAttempts >= 1);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
            host.Dispose();
            temp.Dispose();
        }
    }

    public static async Task UnsupportedConnectIsRejected()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var result = await RunHttp3GeneratedRouteRawHeadersScenarioAsync(
            [
                new Http1HeaderField(":method", "CONNECT"),
                new Http1HeaderField(":authority", "upstream.test:443")
            ]);

        AssertEx.Equal("501", HeaderValue(result.Headers, ":status"));
        AssertEx.True(result.Metrics.Http3RejectedRequests.ContainsKey("connect_unsupported"));
        AssertEx.Equal("", result.UpstreamRequest);
    }

    public static async Task MalformedHttp3ConnectIsRejected()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var result = await RunHttp3GeneratedRouteRawHeadersScenarioAsync(
            [
                new Http1HeaderField(":method", "CONNECT"),
                new Http1HeaderField(":authority", "not/a/tunnel")
            ]);

        AssertEx.Equal("400", HeaderValue(result.Headers, ":status"));
        AssertEx.True(result.Metrics.Http3ProtocolErrors.ContainsKey("invalid_connect_target"));
    }

    public static async Task ExtendedHttp3ConnectWebSocketIsRejected()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        foreach (var protocol in new[] { "websocket", "connect-udp", "webtransport" })
        {
            using var result = await RunHttp3GeneratedRouteRawHeadersScenarioAsync(
                [
                    new Http1HeaderField(":method", "CONNECT"),
                    new Http1HeaderField(":scheme", "https"),
                    new Http1HeaderField(":authority", "localhost"),
                    new Http1HeaderField(":path", "/chat"),
                    new Http1HeaderField(":protocol", protocol)
                ]);

            AssertEx.Equal("400", HeaderValue(result.Headers, ":status"));
            AssertEx.True(result.Metrics.Http3ProtocolErrors.ContainsKey("extended_connect_unsupported"));
            AssertEx.Equal("", result.UpstreamRequest);
        }
    }

    public static async Task Http3PostWithBoundedBodyReachesUpstream()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var result = await RunHttp3ProxyRouteScenarioAsync(
            "POST",
            "/submit?x=1",
            "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 2\r\n\r\nok",
            requestBody: "hello=world");

        AssertEx.Equal("200", HeaderValue(result.Headers, ":status"));
        AssertEx.Equal("ok", result.Body);
        AssertEx.True(result.UpstreamRequest.StartsWith("POST /submit?x=1 HTTP/1.1", StringComparison.Ordinal), result.UpstreamRequest);
        AssertEx.True(result.UpstreamRequest.Contains("Content-Length: 11", StringComparison.OrdinalIgnoreCase), result.UpstreamRequest);
        AssertEx.True(result.UpstreamRequest.EndsWith("hello=world", StringComparison.Ordinal), result.UpstreamRequest);
    }

    public static async Task Http3PutPatchAndDeleteBodiesReachUpstream()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        var temp = TemporaryDirectory.Create();
        var proxyPort = GetFreeTcpUdpPort();
        var upstreamPort = GetFreeTcpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3ProxySite(temp.Path, proxyPort, upstreamPort);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var upstreamTask = RunSequentialResponseUpstreamAsync(
            upstreamPort,
            [
                "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 3\r\n\r\nput",
                "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 5\r\n\r\npatch",
                "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 6\r\n\r\ndelete"
            ],
            timeout.Token);
        var host = BuildProxyHost(temp.Path);
        await host.StartAsync(timeout.Token);

        try
        {
            var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
            await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);
            var put = await SendHttp3RequestAsync(proxyPort, "PUT", "/items/1", timeout.Token, body: "put-body");
            var patch = await SendHttp3RequestAsync(proxyPort, "PATCH", "/items/1", timeout.Token, body: "patch-body");
            var delete = await SendHttp3RequestAsync(proxyPort, "DELETE", "/items/1", timeout.Token, body: "delete-body");
            var upstreamRequests = await upstreamTask.WaitAsync(timeout.Token);

            AssertEx.Equal("put", put.Body);
            AssertEx.Equal("patch", patch.Body);
            AssertEx.Equal("delete", delete.Body);
            AssertEx.True(upstreamRequests[0].StartsWith("PUT /items/1 HTTP/1.1", StringComparison.Ordinal), upstreamRequests[0]);
            AssertEx.True(upstreamRequests[0].EndsWith("put-body", StringComparison.Ordinal), upstreamRequests[0]);
            AssertEx.True(upstreamRequests[1].StartsWith("PATCH /items/1 HTTP/1.1", StringComparison.Ordinal), upstreamRequests[1]);
            AssertEx.True(upstreamRequests[1].EndsWith("patch-body", StringComparison.Ordinal), upstreamRequests[1]);
            AssertEx.True(upstreamRequests[2].StartsWith("DELETE /items/1 HTTP/1.1", StringComparison.Ordinal), upstreamRequests[2]);
            AssertEx.True(upstreamRequests[2].EndsWith("delete-body", StringComparison.Ordinal), upstreamRequests[2]);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
            host.Dispose();
            temp.Dispose();
        }
    }

    public static async Task Http3PathRewriteAppliesToProxyRoute()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var result = await RunHttp3ProxyRouteScenarioAsync(
            "GET",
            "/public/api/users?id=1",
            "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 2\r\n\r\nok",
            routeExtraJson:
            """
                  "pathRewrite": {
                    "stripPrefix": "/public"
                  },
            """);

        AssertEx.True(
            result.UpstreamRequest.StartsWith("GET /api/users?id=1 HTTP/1.1", StringComparison.Ordinal),
            result.UpstreamRequest);
    }

    public static async Task Http3BodySizeLimitApplies()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var result = await RunHttp3ProxyRouteScenarioAsync(
            "POST",
            "/too-large",
            "",
            requestBody: "too-large",
            routeExtraJson:
            """
                  "overrides": {
                    "maxRequestBodyBytes": 4
                  },
            """);

        AssertEx.Equal("413", HeaderValue(result.Headers, ":status"));
        AssertEx.Equal("Payload Too Large", result.Body);
        AssertEx.Equal("", result.UpstreamRequest);
        AssertEx.True(result.Metrics.Http3RejectedRequests.ContainsKey("request_body_too_large"));
    }

    public static async Task Http3LegacyBufferedRequestBodyLimitDoesNotBlockStreaming()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var result = await RunHttp3ProxyRouteScenarioAsync(
            "POST",
            "/streamed-body",
            "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 8\r\n\r\naccepted",
            requestBody: "buffered",
            listenerExtraJson:
            """
                  "http3MaxBufferedRequestBodyBytes": 4,
            """);

        AssertEx.Equal("200", HeaderValue(result.Headers, ":status"));
        AssertEx.Equal("accepted", result.Body);
        AssertEx.True(result.UpstreamRequest.Contains("Content-Length: 8", StringComparison.OrdinalIgnoreCase), result.UpstreamRequest);
        AssertEx.True(result.UpstreamRequest.EndsWith("buffered", StringComparison.Ordinal), result.UpstreamRequest);
        AssertEx.False(result.Metrics.Http3RejectedRequests.ContainsKey("request_body_too_large"));
    }

    public static async Task Http3RequestWithBodyIsNotRetried()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        var temp = TemporaryDirectory.Create();
        var proxyPort = GetFreeTcpUdpPort();
        var firstUpstreamPort = GetFreeTcpPort();
        var secondUpstreamPort = GetFreeTcpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3RetrySite(temp.Path, proxyPort, firstUpstreamPort, secondUpstreamPort);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var host = BuildProxyHost(temp.Path);
        await host.StartAsync(timeout.Token);

        try
        {
            var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
            await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);
            var response = await SendHttp3RequestAsync(proxyPort, "GET", "/retry-body", timeout.Token, body: "not-replayable");
            var metrics = host.Services.GetRequiredService<ProxyMetrics>().Snapshot();

            var status = HeaderValue(response.Headers, ":status");
            AssertEx.True(status is "502" or "504", status);
            AssertEx.True(metrics.RetrySkipped.Any(static skipped => skipped.Reason == "request_body"));
            AssertEx.Equal(0L, metrics.RetryAttempts);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
            host.Dispose();
            temp.Dispose();
        }
    }

    public static async Task InvalidFrameSequenceIsRejected()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var result = await RunHttp3GeneratedRouteScenarioAsync("GET", "/sequence", "unused", dataBeforeHeaders: true);

        AssertEx.Equal("400", HeaderValue(result.Headers, ":status"));
        AssertEx.True(result.Metrics.Http3ProtocolErrors.ContainsKey("unexpected_data"));
    }

    public static async Task UnexpectedControlFrameOnRequestStreamIsRejected()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var result = await RunHttp3GeneratedRouteScenarioAsync(
            "GET",
            "/control",
            "unused",
            settingsAfterHeaders: true);

        AssertEx.Equal("400", HeaderValue(result.Headers, ":status"));
        AssertEx.True(result.Metrics.Http3ProtocolErrors.ContainsKey("unexpected_control_frame"));
    }

    public static async Task GoAwayFrameOnRequestStreamIsRejected()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var result = await RunHttp3GeneratedRouteScenarioAsync(
            "GET",
            "/goaway",
            "unused",
            goAwayAfterHeaders: true);

        AssertEx.Equal("400", HeaderValue(result.Headers, ":status"));
        AssertEx.True(result.Metrics.Http3ProtocolErrors.ContainsKey("unexpected_control_frame"));
    }

    public static async Task DuplicateHeadersAfterHeadersIsRejected()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var result = await RunHttp3GeneratedRouteScenarioAsync(
            "GET",
            "/duplicate",
            "unused",
            duplicateHeadersAfterHeaders: true);

        AssertEx.Equal("400", HeaderValue(result.Headers, ":status"));
        AssertEx.True(result.Metrics.Http3ProtocolErrors.ContainsKey("duplicate_headers"));
        AssertEx.Equal(0L, result.Metrics.ActiveHttp3Streams);
    }

    public static async Task UnknownFrameBeforeHeadersIsRejected()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var result = await RunHttp3GeneratedRouteScenarioAsync(
            "GET",
            "/unknown",
            "unused",
            unknownFrameBeforeHeaders: true);

        AssertEx.Equal("400", HeaderValue(result.Headers, ":status"));
        AssertEx.True(result.Metrics.Http3ProtocolErrors.ContainsKey("unsupported_frame"));
        AssertEx.Equal(0L, result.Metrics.ActiveHttp3Streams);
    }

    public static async Task MaxPushFrameOnRequestStreamIsRejected()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var result = await RunHttp3GeneratedRouteScenarioAsync(
            "GET",
            "/max-push",
            "unused",
            maxPushAfterHeaders: true);

        AssertEx.Equal("400", HeaderValue(result.Headers, ":status"));
        AssertEx.True(result.Metrics.Http3ProtocolErrors.ContainsKey("unexpected_control_frame"));
        AssertEx.Equal(0L, result.Metrics.ActiveHttp3Streams);
    }

    public static async Task StreamLevelProtocolErrorDoesNotPoisonConnection()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var temp = TemporaryDirectory.Create();
        var port = GetFreeTcpUdpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3Site(temp.Path, port, "http3Preview", staticBody: "still-open");
        using var host = BuildProxyHost(temp.Path);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StartAsync(timeout.Token);

        var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
        await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);
        await using var connection = await ConnectHttp3Async(port, timeout.Token);
        await using (var badStream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, timeout.Token))
        {
            using var request = new MemoryStream();
            Http3PreviewCodec.WriteFrame(request, Http3PreviewCodec.DataFrame, ReadOnlySpan<byte>.Empty);
            await badStream.WriteAsync(request.ToArray(), completeWrites: true, timeout.Token);
            var badResponse = DecodeHttp3Response(await ReadToEndAsync(badStream, timeout.Token));
            AssertEx.Equal("400", HeaderValue(badResponse.Headers, ":status"));
        }

        await using (var goodStream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, timeout.Token))
        {
            await WriteHttp3RequestAsync(goodStream, "GET", "/ok", null, timeout.Token);
            var goodResponse = DecodeHttp3Response(await ReadToEndAsync(goodStream, timeout.Token));
            AssertEx.Equal("200", HeaderValue(goodResponse.Headers, ":status"));
            AssertEx.Equal("still-open", goodResponse.Body);
        }

        await connection.CloseAsync(0, CancellationToken.None);
    }

    public static async Task ConcurrentStreamResetDoesNotLeakActiveStreams()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var temp = TemporaryDirectory.Create();
        var port = GetFreeTcpUdpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3Site(temp.Path, port, "http3Preview", staticBody: "good");
        using var host = BuildProxyHost(temp.Path);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StartAsync(timeout.Token);

        try
        {
            var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
            await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);
            await using var connection = await ConnectHttp3Async(port, timeout.Token);
            await using var badStream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, timeout.Token);
            await using var goodStream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, timeout.Token);

            using var badRequest = new MemoryStream();
            Http3PreviewCodec.WriteFrame(badRequest, Http3PreviewCodec.DataFrame, ReadOnlySpan<byte>.Empty);
            await badStream.WriteAsync(badRequest.ToArray(), completeWrites: true, timeout.Token);
            await WriteHttp3RequestAsync(goodStream, "GET", "/good", null, timeout.Token);

            var badResponse = DecodeHttp3Response(await ReadToEndAsync(badStream, timeout.Token));
            var goodResponse = DecodeHttp3Response(await ReadToEndAsync(goodStream, timeout.Token));
            var metricsStore = host.Services.GetRequiredService<ProxyMetrics>();
            await WaitForHttp3StreamsToDrainAsync(metricsStore, timeout.Token);
            var metrics = metricsStore.Snapshot();

            AssertEx.Equal("400", HeaderValue(badResponse.Headers, ":status"));
            AssertEx.Equal("200", HeaderValue(goodResponse.Headers, ":status"));
            AssertEx.Equal("good", goodResponse.Body);
            AssertEx.Equal(0L, metrics.ActiveHttp3Streams);
            await connection.CloseAsync(0, CancellationToken.None);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    public static async Task QpackDecodeFailureDoesNotReachRouteSelection()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var result = await RunHttp3GeneratedRouteRawHeaderBlockScenarioAsync([0, 0, 0x80]);

        AssertEx.Equal("400", HeaderValue(result.Headers, ":status"));
        AssertEx.Equal("Bad Request", result.Body);
        AssertEx.Equal(0L, result.Metrics.Http3Requests);
        AssertEx.False(result.Metrics.Http3GeneratedResponses > 0);
        AssertEx.True(result.Metrics.Http3ProtocolErrors.ContainsKey("unsupported_qpack_index"));
    }

    public static async Task ProtocolErrorBudgetClosesAbusiveConnection()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var temp = TemporaryDirectory.Create();
        var port = GetFreeTcpUdpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3Site(temp.Path, port, "http3Preview", staticBody: "budget");
        using var host = BuildProxyHost(temp.Path);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StartAsync(timeout.Token);

        try
        {
            var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
            await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);
            await using var connection = await ConnectHttp3Async(port, timeout.Token);
            for (var index = 0; index < 8; index++)
            {
                await using var stream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, timeout.Token);
                using var request = new MemoryStream();
                Http3PreviewCodec.WriteFrame(request, Http3PreviewCodec.DataFrame, ReadOnlySpan<byte>.Empty);
                await stream.WriteAsync(request.ToArray(), completeWrites: true, timeout.Token);
                _ = await ReadToEndAsync(stream, timeout.Token);
            }

            var metricsStore = host.Services.GetRequiredService<ProxyMetrics>();
            await WaitForHttp3StreamsToDrainAsync(metricsStore, timeout.Token);
            var metrics = metricsStore.Snapshot();

            AssertEx.True(metrics.Http3ProtocolErrors.TryGetValue("unexpected_data", out var errors), "missing unexpected_data metric");
            AssertEx.True(errors >= 8);
            AssertEx.Equal(0L, metrics.ActiveHttp3Streams);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    public static void OversizedHeaderBlockIsRejected()
    {
        var headerBlock = Http3PreviewCodec.EncodeHeaderBlock(
        [
            new Http1HeaderField(":method", "GET"),
            new Http1HeaderField(":scheme", "https"),
            new Http1HeaderField(":authority", "localhost"),
            new Http1HeaderField(":path", "/"),
            new Http1HeaderField("x-large", new string('a', 256))
        ]);

        var ok = Http3PreviewCodec.TryDecodeHeaderBlock(
            headerBlock,
            maxHeaderBytes: 32,
            out _,
            out var reason);

        AssertEx.False(ok);
        AssertEx.Equal("header_list_too_large", reason);
    }

    public static void QpackHeaderBlockAtExactLimitIsAccepted()
    {
        var headerBlock = Http3PreviewCodec.EncodeHeaderBlock(
        [
            new Http1HeaderField(":method", "GET"),
            new Http1HeaderField(":scheme", "https"),
            new Http1HeaderField(":authority", "localhost"),
            new Http1HeaderField(":path", "/boundary"),
            new Http1HeaderField("x-boundary", "ok")
        ]);

        var ok = Http3PreviewCodec.TryDecodeHeaderBlock(
            headerBlock,
            maxHeaderBytes: headerBlock.Length,
            out var headers,
            out var reason);

        AssertEx.True(ok, reason);
        AssertEx.Equal("/boundary", headers.Single(static header => header.Name == ":path").Value);
    }

    public static void UnsupportedQpackDynamicTableUsageIsRejected()
    {
        var block = new byte[] { 0, 0, 0x80 };

        var ok = Http3PreviewCodec.TryDecodeHeaderBlock(
            block,
            maxHeaderBytes: 32,
            out _,
            out var reason);

        AssertEx.False(ok);
        AssertEx.Equal("unsupported_qpack_index", reason);
    }

    public static void InvalidQpackStaticTableReferenceIsRejected()
    {
        var block = new byte[] { 0, 0, 0xff, 0x7f };

        var ok = Http3PreviewCodec.TryDecodeHeaderBlock(
            block,
            maxHeaderBytes: 1024,
            out _,
            out var reason);

        AssertEx.False(ok);
        AssertEx.Equal("unsupported_qpack_index", reason);
    }

    public static void UnsupportedQpackDynamicTablePrefixIsRejected()
    {
        var block = new byte[] { 1, 0 };

        var ok = Http3PreviewCodec.TryDecodeHeaderBlock(
            block,
            maxHeaderBytes: 32,
            out _,
            out var reason);

        AssertEx.False(ok);
        AssertEx.Equal("unsupported_qpack_dynamic_table", reason);
    }

    public static void QpackHuffmanStaticNameReferenceDecodes()
    {
        var block = new byte[]
        {
            0x00, 0x00, 0x50, 0x8c, 0xf1, 0xe3, 0xc2, 0xe5,
            0xf2, 0x3a, 0x6b, 0xa0, 0xab, 0x90, 0xf4, 0xff
        };

        var ok = Http3PreviewCodec.TryDecodeHeaderBlock(
            block,
            maxHeaderBytes: 1024,
            out var headers,
            out var reason);

        AssertEx.True(ok, reason);
        AssertEx.Equal(":authority", headers[0].Name);
        AssertEx.Equal("www.example.com", headers[0].Value);
    }

    public static void MalformedPseudoHeadersAreRejected()
    {
        var headers = new[]
        {
            new Http1HeaderField(":method", "GET"),
            new Http1HeaderField(":method", "HEAD"),
            new Http1HeaderField(":scheme", "https"),
            new Http1HeaderField(":authority", "localhost"),
            new Http1HeaderField(":path", "/")
        };

        var ok = Http3PreviewRequestTranslator.TryBuildRequest(
            headers,
            LegacyHttp3Listener("http3Preview", experimental: true),
            out _,
            out var reason);

        AssertEx.False(ok);
        AssertEx.Equal("invalid_pseudo_header", reason);
    }

    public static void PseudoHeaderAfterRegularHeaderIsRejected()
    {
        var headers = new[]
        {
            new Http1HeaderField(":method", "GET"),
            new Http1HeaderField(":scheme", "https"),
            new Http1HeaderField("x-before", "1"),
            new Http1HeaderField(":authority", "localhost"),
            new Http1HeaderField(":path", "/")
        };

        var ok = Http3PreviewRequestTranslator.TryBuildRequest(
            headers,
            LegacyHttp3Listener("http3Preview", experimental: true),
            out _,
            out var reason);

        AssertEx.False(ok);
        AssertEx.Equal("invalid_pseudo_header", reason);
    }

    public static void ForbiddenPseudoHeaderIsRejected()
    {
        var headers = new[]
        {
            new Http1HeaderField(":method", "GET"),
            new Http1HeaderField(":scheme", "https"),
            new Http1HeaderField(":authority", "localhost"),
            new Http1HeaderField(":path", "/"),
            new Http1HeaderField(":status", "200")
        };

        var ok = Http3PreviewRequestTranslator.TryBuildRequest(
            headers,
            LegacyHttp3Listener("http3Preview", experimental: true),
            out _,
            out var reason);

        AssertEx.False(ok);
        AssertEx.Equal("invalid_pseudo_header", reason);
    }

    public static void MissingPseudoHeadersAreRejected()
    {
        var headers = new[]
        {
            new Http1HeaderField(":method", "GET"),
            new Http1HeaderField(":scheme", "https"),
            new Http1HeaderField(":authority", "localhost")
        };

        var ok = Http3PreviewRequestTranslator.TryBuildRequest(
            headers,
            LegacyHttp3Listener("http3Preview", experimental: true),
            out _,
            out var reason);

        AssertEx.False(ok);
        AssertEx.Equal("missing_pseudo_header", reason);
    }

    public static void ForbiddenConnectionHeadersAreRejected()
    {
        var headers = new[]
        {
            new Http1HeaderField(":method", "GET"),
            new Http1HeaderField(":scheme", "https"),
            new Http1HeaderField(":authority", "localhost"),
            new Http1HeaderField(":path", "/"),
            new Http1HeaderField("connection", "close")
        };

        var ok = Http3PreviewRequestTranslator.TryBuildRequest(
            headers,
            LegacyHttp3Listener("http3Preview", experimental: true),
            out _,
            out var reason);

        AssertEx.False(ok);
        AssertEx.Equal("forbidden_header", reason);
    }

    public static void InvalidRegularHeaderNameIsRejected()
    {
        var headers = new[]
        {
            new Http1HeaderField(":method", "GET"),
            new Http1HeaderField(":scheme", "https"),
            new Http1HeaderField(":authority", "localhost"),
            new Http1HeaderField(":path", "/"),
            new Http1HeaderField("bad header", "value")
        };

        var ok = Http3PreviewRequestTranslator.TryBuildRequest(
            headers,
            LegacyHttp3Listener("http3Preview", experimental: true),
            out _,
            out var reason);

        AssertEx.False(ok);
        AssertEx.Equal("invalid_header_name", reason);
    }

    public static void InvalidPseudoHeaderValuesAreRejected()
    {
        var headers = new[]
        {
            new Http1HeaderField(":method", "GE T"),
            new Http1HeaderField(":scheme", "https"),
            new Http1HeaderField(":authority", "local/host"),
            new Http1HeaderField(":path", "/")
        };

        var ok = Http3PreviewRequestTranslator.TryBuildRequest(
            headers,
            LegacyHttp3Listener("http3Preview", experimental: true),
            out _,
            out var reason);

        AssertEx.False(ok);
        AssertEx.Equal("invalid_method", reason);
    }

    public static void MalformedAuthorityAndPathAreRejected()
    {
        var badAuthority = new[]
        {
            new Http1HeaderField(":method", "GET"),
            new Http1HeaderField(":scheme", "https"),
            new Http1HeaderField(":authority", "local?host"),
            new Http1HeaderField(":path", "/")
        };
        var badPath = new[]
        {
            new Http1HeaderField(":method", "GET"),
            new Http1HeaderField(":scheme", "https"),
            new Http1HeaderField(":authority", "localhost"),
            new Http1HeaderField(":path", "/fragment#bad")
        };

        var authorityOk = Http3PreviewRequestTranslator.TryBuildRequest(
            badAuthority,
            LegacyHttp3Listener("http3Preview", experimental: true),
            out _,
            out var authorityReason);
        var pathOk = Http3PreviewRequestTranslator.TryBuildRequest(
            badPath,
            LegacyHttp3Listener("http3Preview", experimental: true),
            out _,
            out var pathReason);

        AssertEx.False(authorityOk);
        AssertEx.Equal("invalid_target", authorityReason);
        AssertEx.False(pathOk);
        AssertEx.Equal("invalid_target", pathReason);
    }

    public static void ConnectSpecificPseudoHeaderRulesAreEnforced()
    {
        var connectWithPath = new[]
        {
            new Http1HeaderField(":method", "CONNECT"),
            new Http1HeaderField(":authority", "upstream.test:443"),
            new Http1HeaderField(":path", "/")
        };
        var connectWithBody = new[]
        {
            new Http1HeaderField(":method", "CONNECT"),
            new Http1HeaderField(":authority", "upstream.test:443"),
            new Http1HeaderField("content-length", "1")
        };

        var pathOk = Http3PreviewRequestTranslator.TryBuildRequest(
            connectWithPath,
            LegacyHttp3Listener("http3Preview", experimental: true),
            out _,
            out var pathReason);
        var bodyOk = Http3PreviewRequestTranslator.TryBuildRequest(
            connectWithBody,
            LegacyHttp3Listener("http3Preview", experimental: true),
            out _,
            out var bodyReason);

        AssertEx.False(pathOk);
        AssertEx.Equal("malformed_connect", pathReason);
        AssertEx.False(bodyOk);
        AssertEx.Equal("connect_body_unsupported", bodyReason);
    }

    public static void MetricsIncludeHttp3Counters()
    {
        var metrics = new ProxyMetrics();
        metrics.QuicListenerStarted();
        metrics.Http3ConnectionAccepted();
        metrics.Http3ConnectionClosed();
        metrics.Http3RequestReceived();
        metrics.Http3RequestCompleted("GET", 200, "success");
        metrics.Http3ProxiedRequest();
        metrics.Http3GeneratedResponse();
        metrics.Http3StreamStarted();
        metrics.Http3StreamEnded();
        metrics.Http3StreamReset();
        metrics.Http3StreamedResponse();
        metrics.Http3ResponseStreamStarted();
        metrics.Http3ResponseStreamEnded();
        metrics.AddHttp3ResponseBytesSent(12);
        metrics.AddHttp3RequestBodyBytesReceived(5);
        metrics.Http3ResponseStreamReset();
        metrics.Http3AltSvcEmitted();
        metrics.Http3AltSvcSuppressed();
        metrics.Http3RequestRejected("method_unsupported");
        metrics.Http3ProtocolError("invalid_frame");
        metrics.SetActiveQuicListeners(1);
        var snapshot = metrics.Snapshot();

        AssertEx.Equal(1L, snapshot.QuicListenerStartSuccesses);
        AssertEx.Equal(1L, snapshot.Http3AcceptedConnections);
        AssertEx.Equal(0L, snapshot.ActiveHttp3Connections);
        AssertEx.Equal(1L, snapshot.Http3Requests);
        AssertEx.Equal(1L, snapshot.Http3RequestsByOutcome.Single(static item => item.Method == "GET" && item.Outcome == "success" && item.StatusClass == "2xx").Count);
        AssertEx.Equal(1L, snapshot.Http3ProxiedRequests);
        AssertEx.Equal(1L, snapshot.Http3GeneratedResponses);
        AssertEx.Equal(0L, snapshot.ActiveHttp3Streams);
        AssertEx.Equal(1L, snapshot.Http3StreamResets);
        AssertEx.Equal(1L, snapshot.Http3StreamedResponses);
        AssertEx.Equal(0L, snapshot.ActiveHttp3ResponseStreams);
        AssertEx.Equal(12L, snapshot.Http3ResponseBytesSent);
        AssertEx.Equal(5L, snapshot.Http3RequestBodyBytesReceived);
        AssertEx.Equal(1L, snapshot.Http3ResponseStreamResets);
        AssertEx.Equal(1L, snapshot.Http3AltSvcEmitted);
        AssertEx.Equal(1L, snapshot.Http3AltSvcSuppressed);
        AssertEx.Equal(1L, snapshot.Http3RejectedRequests["method_unsupported"]);
        AssertEx.Equal(1L, snapshot.Http3ProtocolErrors["invalid_frame"]);
        AssertEx.Equal(1L, snapshot.ActiveQuicListeners);
    }

    public static void ConfigLintReportsHttp3DefaultReadinessIssues()
    {
        var service = new ConfigLintService(
            new ProxyConfigurationStore(),
            new ProxyRuntimeState(),
            new SiteConfigurationParser(),
            new ProxyOptionsValidator(),
            new ProxyMetrics(),
            TimeProvider.System);
        var result = service.LintSubmitted(new ConfigLintRequest(
            "json",
            """
            {
              "name": "http3",
              "listeners": [
                {
                  "name": "main",
                  "address": "127.0.0.1",
                  "port": 8443,
                  "transport": "https",
                  "protocols": "http3Preview",
                  "experimentalHttp3": true,
                  "http3Enablement": "preview",
                  "http3MaxBufferedRequestBodyBytes": 4096,
                  "defaultCertificateId": "home-cert"
                }
              ],
              "host": "localhost",
              "routes": [
                {
                  "name": "static",
                  "pathPrefix": "/",
                  "action": "staticResponse",
                  "staticResponse": {
                    "statusCode": 200,
                    "contentType": "text/plain",
                    "body": "ok"
                  }
                }
              ]
            }
            """));
        var codes = result.Findings.Select(static finding => finding.Code).ToArray();

        AssertEx.True(codes.Contains("http3_alt_svc_not_ready"));
        AssertEx.True(codes.Contains("http3_legacy_buffer_limit_configured"));
        AssertEx.False(codes.Contains("http3_default_readiness_buffered_body"));
        AssertEx.False(codes.Contains("http3_default_readiness_qpack_static_only"));
    }

    private static async Task<Http3ScenarioResult> RunHttp3GeneratedRouteScenarioAsync(
        string method,
        string target,
        string body,
        bool includeBodyData = false,
        bool dataBeforeHeaders = false,
        bool settingsAfterHeaders = false,
        bool goAwayAfterHeaders = false,
        bool duplicateHeadersAfterHeaders = false,
        bool unknownFrameBeforeHeaders = false,
        bool maxPushAfterHeaders = false,
        string? routeJson = null)
    {
        var temp = TemporaryDirectory.Create();
        var port = GetFreeTcpUdpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3Site(temp.Path, port, "http3Preview", body, routeJson: routeJson);
        var host = BuildProxyHost(temp.Path);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StartAsync(timeout.Token);

        var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
        await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);
        var response = await SendHttp3RequestAsync(
            port,
            method,
            target,
            timeout.Token,
            includeBodyData: includeBodyData,
            dataBeforeHeaders: dataBeforeHeaders,
            settingsAfterHeaders: settingsAfterHeaders,
            goAwayAfterHeaders: goAwayAfterHeaders,
            duplicateHeadersAfterHeaders: duplicateHeadersAfterHeaders,
            unknownFrameBeforeHeaders: unknownFrameBeforeHeaders,
            maxPushAfterHeaders: maxPushAfterHeaders);
        var metricsStore = host.Services.GetRequiredService<ProxyMetrics>();
        await WaitForHttp3StreamsToDrainAsync(metricsStore, timeout.Token);
        var metrics = metricsStore.Snapshot();
        return new Http3ScenarioResult(temp, host, response.Headers, response.Body, metrics, "");
    }

    private static async Task<Http3ScenarioResult> RunHttp3GeneratedRouteRawHeaderBlockScenarioAsync(
        byte[] headerBlock)
    {
        var temp = TemporaryDirectory.Create();
        var port = GetFreeTcpUdpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3Site(temp.Path, port, "http3Preview", "unused");
        var host = BuildProxyHost(temp.Path);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StartAsync(timeout.Token);

        var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
        await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);
        var response = await SendHttp3RawHeaderBlockAsync(port, headerBlock, timeout.Token);
        var metricsStore = host.Services.GetRequiredService<ProxyMetrics>();
        await WaitForHttp3StreamsToDrainAsync(metricsStore, timeout.Token);
        var metrics = metricsStore.Snapshot();
        return new Http3ScenarioResult(temp, host, response.Headers, response.Body, metrics, "");
    }

    private static async Task<Http3ScenarioResult> RunHttp3GeneratedRouteRawHeadersScenarioAsync(
        IReadOnlyList<Http1HeaderField> headers)
    {
        var temp = TemporaryDirectory.Create();
        var port = GetFreeTcpUdpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3Site(temp.Path, port, "http3Preview", "unused");
        var host = BuildProxyHost(temp.Path);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StartAsync(timeout.Token);

        var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
        await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);
        var response = await SendHttp3RequestAsync(port, headers, timeout.Token);
        var metricsStore = host.Services.GetRequiredService<ProxyMetrics>();
        await WaitForHttp3StreamsToDrainAsync(metricsStore, timeout.Token);
        var metrics = metricsStore.Snapshot();
        return new Http3ScenarioResult(temp, host, response.Headers, response.Body, metrics, "");
    }

    private static async Task<Http3ScenarioResult> RunHttp3ProxyRouteScenarioAsync(
        string method,
        string target,
        string upstreamResponse,
        string? requestBody = null,
        string routeExtraJson = "",
        string listenerExtraJson = "")
    {
        var temp = TemporaryDirectory.Create();
        var proxyPort = GetFreeTcpUdpPort();
        var upstreamPort = GetFreeTcpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3ProxySite(temp.Path, proxyPort, upstreamPort, routeExtraJson, listenerExtraJson);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var upstreamTask = string.IsNullOrEmpty(upstreamResponse)
            ? Task.FromResult("")
            : RunSingleResponseUpstreamAsync(upstreamPort, upstreamResponse, timeout.Token);
        var host = BuildProxyHost(temp.Path);
        await host.StartAsync(timeout.Token);

        var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
        await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);
        var response = await SendHttp3RequestAsync(proxyPort, method, target, timeout.Token, body: requestBody);
        var upstreamRequest = await upstreamTask.WaitAsync(timeout.Token);
        var metricsStore = host.Services.GetRequiredService<ProxyMetrics>();
        await WaitForHttp3StreamsToDrainAsync(metricsStore, timeout.Token);
        var metrics = metricsStore.Snapshot();
        return new Http3ScenarioResult(temp, host, response.Headers, response.Body, metrics, upstreamRequest);
    }

    private static async Task<Http3Response> SendHttp3RequestAsync(
        int port,
        string method,
        string target,
        CancellationToken cancellationToken,
        bool includeBodyData = false,
        bool dataBeforeHeaders = false,
        string? body = null,
        bool settingsAfterHeaders = false,
        bool goAwayAfterHeaders = false,
        bool duplicateHeadersAfterHeaders = false,
        bool unknownFrameBeforeHeaders = false,
        bool maxPushAfterHeaders = false,
        Action<string>? certificateSubjectObserver = null)
    {
        await using var connection = await ConnectHttp3Async(port, cancellationToken, certificateSubjectObserver);

        await using var stream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, cancellationToken);
        await WriteHttp3RequestAsync(
            stream,
            method,
            target,
            body ?? (includeBodyData ? "body" : null),
            cancellationToken,
            dataBeforeHeaders,
            settingsAfterHeaders,
            goAwayAfterHeaders,
            duplicateHeadersAfterHeaders,
            unknownFrameBeforeHeaders,
            maxPushAfterHeaders);

        var responseBytes = await ReadToEndAsync(stream, cancellationToken);
        var response = DecodeHttp3Response(responseBytes);
        await connection.CloseAsync(0, CancellationToken.None);
        return response;
    }

    private static async Task<Http3Response> SendHttp3RequestAsync(
        int port,
        IReadOnlyList<Http1HeaderField> headers,
        CancellationToken cancellationToken)
    {
        await using var connection = await ConnectHttp3Async(port, cancellationToken);
        await using var stream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, cancellationToken);
        await WriteHttp3RequestAsync(stream, headers, body: null, cancellationToken);

        var responseBytes = await ReadToEndAsync(stream, cancellationToken);
        var response = DecodeHttp3Response(responseBytes);
        await connection.CloseAsync(0, CancellationToken.None);
        return response;
    }

    private static async Task<Http3Response> SendHttp3RawHeaderBlockAsync(
        int port,
        byte[] headerBlock,
        CancellationToken cancellationToken)
    {
        await using var connection = await ConnectHttp3Async(port, cancellationToken);
        await using var stream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, cancellationToken);
        using var request = new MemoryStream();
        Http3PreviewCodec.WriteFrame(request, Http3PreviewCodec.HeadersFrame, headerBlock);
        await stream.WriteAsync(request.ToArray(), completeWrites: true, cancellationToken);

        var responseBytes = await ReadToEndAsync(stream, cancellationToken);
        var response = DecodeHttp3Response(responseBytes);
        await connection.CloseAsync(0, CancellationToken.None);
        return response;
    }

    private static async ValueTask WriteHttp3RequestAsync(
        QuicStream stream,
        string method,
        string target,
        string? body,
        CancellationToken cancellationToken,
        bool dataBeforeHeaders = false,
        bool settingsAfterHeaders = false,
        bool goAwayAfterHeaders = false,
        bool duplicateHeadersAfterHeaders = false,
        bool unknownFrameBeforeHeaders = false,
        bool maxPushAfterHeaders = false)
    {
        List<Http1HeaderField> requestHeaders =
        [
            new Http1HeaderField(":method", method),
            new Http1HeaderField(":scheme", "https"),
            new Http1HeaderField(":authority", "localhost"),
            new Http1HeaderField(":path", target)
        ];
        if (body is not null)
        {
            requestHeaders.Add(new Http1HeaderField("content-length", Encoding.UTF8.GetByteCount(body).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        await WriteHttp3RequestAsync(
            stream,
            requestHeaders,
            body,
            cancellationToken,
            dataBeforeHeaders,
            settingsAfterHeaders,
            goAwayAfterHeaders,
            duplicateHeadersAfterHeaders,
            unknownFrameBeforeHeaders,
            maxPushAfterHeaders);
    }

    private static async ValueTask WriteHttp3RequestAsync(
        QuicStream stream,
        IReadOnlyList<Http1HeaderField> requestHeaders,
        string? body,
        CancellationToken cancellationToken,
        bool dataBeforeHeaders = false,
        bool settingsAfterHeaders = false,
        bool goAwayAfterHeaders = false,
        bool duplicateHeadersAfterHeaders = false,
        bool unknownFrameBeforeHeaders = false,
        bool maxPushAfterHeaders = false)
    {
        var headerBlock = Http3PreviewCodec.EncodeHeaderBlock(requestHeaders);
        using var request = new MemoryStream();
        if (unknownFrameBeforeHeaders)
        {
            Http3PreviewCodec.WriteFrame(request, 0x21, ReadOnlySpan<byte>.Empty);
        }

        if (dataBeforeHeaders)
        {
            Http3PreviewCodec.WriteFrame(request, Http3PreviewCodec.DataFrame, ReadOnlySpan<byte>.Empty);
        }

        Http3PreviewCodec.WriteFrame(request, Http3PreviewCodec.HeadersFrame, headerBlock);
        if (duplicateHeadersAfterHeaders)
        {
            Http3PreviewCodec.WriteFrame(request, Http3PreviewCodec.HeadersFrame, headerBlock);
        }

        if (settingsAfterHeaders)
        {
            Http3PreviewCodec.WriteFrame(request, Http3PreviewCodec.SettingsFrame, ReadOnlySpan<byte>.Empty);
        }

        if (goAwayAfterHeaders)
        {
            using var goAwayPayload = new MemoryStream();
            Http3PreviewCodec.WriteVarInt(goAwayPayload, 0);
            Http3PreviewCodec.WriteFrame(request, Http3PreviewCodec.GoAwayFrame, goAwayPayload.ToArray());
        }

        if (maxPushAfterHeaders)
        {
            Http3PreviewCodec.WriteFrame(request, 0xD, ReadOnlySpan<byte>.Empty);
        }

        if (body is not null)
        {
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            Http3PreviewCodec.WriteFrame(request, Http3PreviewCodec.DataFrame, bodyBytes);
        }

        await stream.WriteAsync(request.ToArray(), completeWrites: true, cancellationToken);
    }

    private static Http3Response DecodeHttp3Response(byte[] responseBytes)
    {
        var offset = 0;
        IReadOnlyList<Http1HeaderField> headers = [];
        var responseBody = "";
        while (offset < responseBytes.Length)
        {
            if (!Http3PreviewCodec.TryReadFrame(responseBytes, ref offset, out var type, out var payload))
            {
                break;
            }

            if (type == Http3PreviewCodec.HeadersFrame)
            {
                AssertEx.True(Http3PreviewCodec.TryDecodeHeaderBlock(payload.Span, 32 * 1024, out headers, out var reason), reason);
            }
            else if (type == Http3PreviewCodec.DataFrame)
            {
                responseBody += Encoding.UTF8.GetString(payload.Span);
            }
        }

        return new Http3Response(headers, responseBody);
    }

    private static async Task<string> ReadFirstHttp3DataAsync(
        QuicStream stream,
        CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[256];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return "";
            }

            memory.Write(buffer, 0, read);
            var bytes = memory.ToArray();
            var offset = 0;
            while (offset < bytes.Length)
            {
                if (!Http3PreviewCodec.TryReadFrame(bytes, ref offset, out var type, out var payload))
                {
                    break;
                }

                if (type == Http3PreviewCodec.DataFrame && payload.Length > 0)
                {
                    return Encoding.UTF8.GetString(payload.Span);
                }
            }
        }
    }

    private static async Task<Http3Response> ReadHttp3ResponseRemainderAsync(
        QuicStream stream,
        CancellationToken cancellationToken)
    {
        return DecodeHttp3Response(await ReadToEndAsync(stream, cancellationToken));
    }

    private static ValueTask<QuicConnection> ConnectHttp3Async(
        int port,
        CancellationToken cancellationToken,
        Action<string>? certificateSubjectObserver = null)
    {
        return QuicConnection.ConnectAsync(
            new QuicClientConnectionOptions
            {
                RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, port),
                ClientAuthenticationOptions = new SslClientAuthenticationOptions
                {
                    TargetHost = "localhost",
                    ApplicationProtocols = [Http3Alpn],
                    RemoteCertificateValidationCallback = (_, certificate, _, _) =>
                    {
                        if (certificate is not null && certificateSubjectObserver is not null)
                        {
                            certificateSubjectObserver(certificate.Subject);
                        }

                        return true;
                    }
                },
                MaxInboundBidirectionalStreams = 4,
                MaxInboundUnidirectionalStreams = 4,
                IdleTimeout = TimeSpan.FromSeconds(5),
                HandshakeTimeout = TimeSpan.FromSeconds(5),
                DefaultCloseErrorCode = 0x100,
                DefaultStreamErrorCode = 0x100
            },
            cancellationToken);
    }

    private static async Task<string> SendHttp1TlsRequestAsync(
        int port,
        string target,
        CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port, cancellationToken);
        await using var tls = new SslStream(client.GetStream(), false, static (_, _, _, _) => true);
        await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = "localhost",
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            ApplicationProtocols = [SslApplicationProtocol.Http11]
        }, cancellationToken);

        await tls.WriteAsync(
            Encoding.ASCII.GetBytes($"GET {target} HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n"),
            cancellationToken);
        return Encoding.ASCII.GetString(await ReadToEndAsync(tls, cancellationToken));
    }

    private static async Task<string> RunSingleResponseUpstreamAsync(
        int upstreamPort,
        string response,
        CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Loopback, upstreamPort);
        listener.Start();
        try
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken);
            await using var stream = client.GetStream();
            var request = await ReadHttp1RequestAsync(stream, cancellationToken);
            await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken);
            return request;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task<string[]> RunSequentialResponseUpstreamAsync(
        int upstreamPort,
        IReadOnlyList<string> responses,
        CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Loopback, upstreamPort);
        listener.Start();
        List<string> requests = [];
        try
        {
            foreach (var response in responses)
            {
                using var client = await listener.AcceptTcpClientAsync(cancellationToken);
                await using var stream = client.GetStream();
                requests.Add(await ReadHttp1RequestAsync(stream, cancellationToken));
                await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken);
            }

            return requests.ToArray();
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task<string> RunStreamingResponseUpstreamAsync(
        int upstreamPort,
        TaskCompletionSource firstChunkSent,
        TaskCompletionSource releaseUpstream,
        CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Loopback, upstreamPort);
        listener.Start();
        try
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken);
            await using var stream = client.GetStream();
            var request = await ReadHttp1RequestAsync(stream, cancellationToken);
            await stream.WriteAsync(
                Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 13\r\n\r\nstream-"),
                cancellationToken);
            firstChunkSent.SetResult();
            await releaseUpstream.Task.WaitAsync(cancellationToken);
            await stream.WriteAsync(Encoding.ASCII.GetBytes("second"), cancellationToken);
            return request;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task<string> ReadHttp1RequestAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[256];
        var headerEnd = -1;
        var contentLength = 0;
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return Encoding.ASCII.GetString(memory.ToArray());
            }

            memory.Write(buffer, 0, read);
            var bytes = memory.ToArray();
            headerEnd = headerEnd < 0 ? IndexOfHeaderEnd(bytes) : headerEnd;
            if (headerEnd >= 0)
            {
                contentLength = contentLength == 0 ? ParseContentLength(bytes, headerEnd) : contentLength;
                if (bytes.Length >= headerEnd + 4 + contentLength)
                {
                    return Encoding.ASCII.GetString(bytes);
                }
            }
        }
    }

    private static int IndexOfHeaderEnd(ReadOnlySpan<byte> bytes)
    {
        for (var index = 3; index < bytes.Length; index++)
        {
            if (bytes[index - 3] == (byte)'\r'
                && bytes[index - 2] == (byte)'\n'
                && bytes[index - 1] == (byte)'\r'
                && bytes[index] == (byte)'\n')
            {
                return index - 3;
            }
        }

        return -1;
    }

    private static int ParseContentLength(byte[] bytes, int headerEnd)
    {
        var head = Encoding.ASCII.GetString(bytes, 0, headerEnd);
        foreach (var line in head.Split("\r\n", StringSplitOptions.None))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0 || !line[..colon].Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return int.TryParse(line[(colon + 1)..].Trim(), out var parsed) && parsed > 0
                ? parsed
                : 0;
        }

        return 0;
    }

    private static async Task<byte[]> ReadToEndAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[4096];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return memory.ToArray();
            }

            memory.Write(buffer, 0, read);
        }
    }

    private static IHost BuildProxyHost(
        string dataDirectory,
        Action<IServiceCollection>? configureServices = null)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(builder =>
            {
                builder.Sources.Clear();
                builder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"{MdravaDataDirectoryOptions.SectionName}:DataDirectory"] = dataDirectory
                });
            })
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureServices((context, services) =>
            {
                services.AddProxyDataPlane(context.Configuration);
                configureServices?.Invoke(services);
            })
            .Build();
    }

    private static RuntimeListener LegacyHttp3Listener(string protocols, bool experimental)
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
            Protocols = protocols.ToLowerInvariant() switch
            {
                "http1andhttp2andhttp3preview" => RuntimeListenerProtocols.Http1AndHttp2AndHttp3Preview,
                "http1andhttp3preview" => RuntimeListenerProtocols.Http1AndHttp3Preview,
                _ => RuntimeListenerProtocols.Http3Preview
            },
            ExperimentalHttp3 = experimental
        };
    }

    private static void WriteCertificateConfig(string dataDirectory)
    {
        var certificatePath = Path.Combine(dataDirectory, "certs", "home.pfx");
        TestCertificates.WriteSelfSignedPfx(certificatePath, "localhost", "secret");
        ConfigurationTests.WriteOperationalConfig(
            dataDirectory,
            certificateId: "home-cert",
            certificatePath: "certs/home.pfx",
            certificatePassword: "secret");
    }

    private static void WriteHttp3Site(
        string dataDirectory,
        int port,
        string protocols,
        string staticBody,
        bool experimental = true,
        bool altSvcEnabled = false,
        int altSvcMaxAgeSeconds = 86400,
        string? http3EnablementOverride = null,
        string? routeJson = null)
    {
        var sites = Directory.CreateDirectory(Path.Combine(dataDirectory, "config", "sites")).FullName;
        var routes = routeJson ??
            $$"""
                {
                  "name": "static",
                  "pathPrefix": "/",
                  "action": "staticResponse",
                  "staticResponse": {
                    "statusCode": 200,
                    "contentType": "text/plain; charset=utf-8",
                    "body": "{{staticBody}}"
                  }
                }
            """;
        var http3Enablement = http3EnablementOverride ?? (protocols.Contains("http3", StringComparison.OrdinalIgnoreCase)
            ? "preview"
            : "disabled");
        File.WriteAllText(
            Path.Combine(sites, "http3.json"),
            $$"""
            {
              "name": "http3",
              "listeners": [
                {
                  "name": "main",
                  "address": "127.0.0.1",
                  "port": {{port}},
                  "transport": "https",
                  "protocols": "{{protocols}}",
                  "experimentalHttp3": {{experimental.ToString().ToLowerInvariant()}},
                  "http3Enablement": "{{http3Enablement}}",
                  "http3AltSvcEnabled": {{altSvcEnabled.ToString().ToLowerInvariant()}},
                  "http3AltSvcMaxAgeSeconds": {{altSvcMaxAgeSeconds}},
                  "defaultCertificateId": "home-cert"
                }
              ],
              "host": "localhost",
              "routes": [
            {{routes}}
              ]
            }
            """);
    }

    private static void WriteHttp3ProxySite(
        string dataDirectory,
        int proxyPort,
        int upstreamPort,
        string routeExtraJson = "",
        string listenerExtraJson = "")
    {
        var sites = Directory.CreateDirectory(Path.Combine(dataDirectory, "config", "sites")).FullName;
        File.WriteAllText(
            Path.Combine(sites, "http3.json"),
            $$"""
            {
              "name": "http3",
              "listeners": [
                {
                  "name": "main",
                  "address": "127.0.0.1",
                  "port": {{proxyPort}},
                  "transport": "https",
                  "protocols": "http3Preview",
                  "experimentalHttp3": true,
                  "http3Enablement": "preview",
            {{listenerExtraJson}}
                  "defaultCertificateId": "home-cert"
                }
              ],
              "host": "localhost",
              "routes": [
                {
                  "name": "proxy",
                  "pathPrefix": "/",
                  "action": "proxy",
            {{routeExtraJson}}
                  "upstreams": [
                    {
                      "name": "local-test",
                      "address": "127.0.0.1",
                      "port": {{upstreamPort}}
                    }
                  ]
                }
              ]
            }
            """);
    }

    private static void WriteHttp3RetrySite(
        string dataDirectory,
        int proxyPort,
        int firstUpstreamPort,
        int secondUpstreamPort)
    {
        var sites = Directory.CreateDirectory(Path.Combine(dataDirectory, "config", "sites")).FullName;
        File.WriteAllText(
            Path.Combine(sites, "http3.json"),
            $$"""
            {
              "name": "http3",
              "listeners": [
                {
                  "name": "main",
                  "address": "127.0.0.1",
                  "port": {{proxyPort}},
                  "transport": "https",
                  "protocols": "http3Preview",
                  "experimentalHttp3": true,
                  "http3Enablement": "preview",
                  "defaultCertificateId": "home-cert"
                }
              ],
              "host": "localhost",
              "routes": [
                {
                  "name": "proxy",
                  "pathPrefix": "/",
                  "action": "proxy",
                  "retry": {
                    "enabled": true,
                    "maxAttempts": 2,
                    "retryOnConnectFailure": true,
                    "retryMethods": [ "GET", "HEAD" ],
                    "retryBackoffMilliseconds": 0
                  },
                  "upstreams": [
                    {
                      "name": "closed",
                      "address": "127.0.0.1",
                      "port": {{firstUpstreamPort}}
                    },
                    {
                      "name": "local-test",
                      "address": "127.0.0.1",
                      "port": {{secondUpstreamPort}}
                    }
                  ]
                }
              ]
            }
            """);
    }

    private static async Task<ProxyListenerStatus> WaitForListenerAsync(
        ProxyRuntimeState runtimeState,
        string name,
        string kind,
        ProxyListenerState state,
        CancellationToken cancellationToken)
    {
        var observed = "";
        while (true)
        {
            var snapshot = runtimeState.Snapshot();
            observed = string.Join(
                ", ",
                snapshot.Listeners.Select(static listener =>
                    $"{listener.Name}/{listener.Kind}/{listener.State}/{listener.LastError ?? "no-error"}"));
            var listener = snapshot.Listeners.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(candidate.Kind, kind, StringComparison.OrdinalIgnoreCase)
                && candidate.State == state);
            if (listener is not null)
            {
                return listener;
            }

            try
            {
                await Task.Delay(25, cancellationToken);
            }
            catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Timed out waiting for listener {name}/{kind}/{state}. Observed listeners: {observed}",
                    exception);
            }
        }
    }

    private static async Task WaitForHttp3StreamsToDrainAsync(ProxyMetrics metrics, CancellationToken cancellationToken)
    {
        while (metrics.Snapshot().ActiveHttp3Streams != 0)
        {
            await Task.Delay(10, cancellationToken);
        }
    }

    private static async Task WaitForNoListenerAsync(
        ProxyRuntimeState runtimeState,
        string name,
        string kind,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            if (!runtimeState.Snapshot().Listeners.Any(candidate =>
                    string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(candidate.Kind, kind, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            await Task.Delay(25, cancellationToken);
        }
    }

    private static int GetFreeTcpUdpPort()
    {
        for (var attempt = 0; attempt < 1000; attempt++)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Socket? udp = null;
            try
            {
                udp = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                udp.Bind(new IPEndPoint(IPAddress.Loopback, port));
                return port;
            }
            catch (SocketException)
            {
            }
            finally
            {
                udp?.Dispose();
                listener.Stop();
            }
        }

        throw new InvalidOperationException("Could not find a free TCP/UDP port pair.");
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static string HeaderValue(IReadOnlyList<Http1HeaderField> headers, string name)
    {
        return headers.First(header => string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private static bool HeaderExists(IReadOnlyList<Http1HeaderField> headers, string name)
    {
        return headers.Any(header => string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FailingQuicListenerFactory : IHttp3QuicListenerFactory
    {
        public bool IsSupported => true;

        public ValueTask<QuicListener> ListenAsync(
            RuntimeListener listener,
            ProxyConfigurationSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            _ = listener;
            _ = snapshot;
            _ = cancellationToken;
            throw new InvalidOperationException("fake_quic_bind_failure");
        }
    }

    private sealed record Http3Response(
        IReadOnlyList<Http1HeaderField> Headers,
        string Body);

    private sealed class Http3ScenarioResult : IDisposable
    {
        private readonly TemporaryDirectory _directory;
        private readonly IHost _host;

        public Http3ScenarioResult(
            TemporaryDirectory directory,
            IHost host,
            IReadOnlyList<Http1HeaderField> headers,
            string body,
            ProxyMetricsSnapshot metrics,
            string upstreamRequest)
        {
            _directory = directory;
            _host = host;
            Headers = headers;
            Body = body;
            Metrics = metrics;
            UpstreamRequest = upstreamRequest;
        }

        public IReadOnlyList<Http1HeaderField> Headers { get; }

        public string Body { get; }

        public ProxyMetricsSnapshot Metrics { get; }

        public string UpstreamRequest { get; }

        public void Dispose()
        {
            try
            {
                _host.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
                _host.Dispose();
            }
            finally
            {
                _directory.Dispose();
            }
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mdrava-h3-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
