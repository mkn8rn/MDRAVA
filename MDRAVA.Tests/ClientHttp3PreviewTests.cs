#pragma warning disable CA1416
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using MDRAVA.API.Controllers;
using MDRAVA.API.Proxy.Configuration.Loading;
using MDRAVA.API.Proxy.Configuration.Paths;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Configuration.Storage;
using MDRAVA.API.Proxy.Configuration;
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

internal static class ClientHttp3PreviewTests
{
    private static readonly SslApplicationProtocol Http3Alpn = new("h3");

    public static void Http3PreviewDisabledByDefault()
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
        AssertEx.False(listener.Http3.EnabledForTraffic);
        AssertEx.Equal("not_configured", listener.Http3.DisabledReason);
    }

    public static void Http3PreviewRequiresExperimentalGate()
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
                        Protocols = "http3Preview",
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

        AssertEx.True(validation.Failed);
        AssertEx.True(AssertEx.NotNull(validation.Failures).Any(static failure => failure.Contains("ExperimentalHttp3", StringComparison.Ordinal)));
    }

    public static void QuicListenerIdentityIsSeparateFromTcpIdentity()
    {
        var listener = PreviewListener("http1AndHttp2AndHttp3Preview", experimental: true);
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

    public static async Task SuccessfulReloadCanAddAndRemovePreviewQuicListener()
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

    public static async Task FailedReloadPreservesOldPreviewQuicListenerSet()
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

    public static void StatusAndEffectiveConfigMarkHttp3AsExperimentalPreview()
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

    public static async Task AltSvcIsAbsentByDefault()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var temp = TemporaryDirectory.Create();
        var port = GetFreeTcpUdpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3Site(temp.Path, port, "http1AndHttp3Preview", staticBody: "alt-default");
        using var host = BuildProxyHost(temp.Path);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await host.StartAsync(timeout.Token);
        try
        {
            var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
            await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);
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
            AssertEx.Equal("preview_only", status.Http3.ReadinessConclusion);
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

        using var result = await RunHttp3GeneratedRouteScenarioAsync("CONNECT", "/tunnel", "unused");

        AssertEx.Equal("501", HeaderValue(result.Headers, ":status"));
        AssertEx.True(result.Metrics.Http3RejectedRequests.ContainsKey("connect_unsupported"));
    }

    public static async Task RequestBodiesRemainRejected()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var result = await RunHttp3GeneratedRouteScenarioAsync("GET", "/body", "unused", includeBodyData: true);

        AssertEx.Equal("400", HeaderValue(result.Headers, ":status"));
        AssertEx.True(result.Metrics.Http3ProtocolErrors.ContainsKey("request_body_unsupported"));
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

            var metrics = host.Services.GetRequiredService<ProxyMetrics>().Snapshot();

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
            PreviewListener("http3Preview", experimental: true),
            out _,
            out var reason);

        AssertEx.False(ok);
        AssertEx.Equal("invalid_pseudo_header", reason);
    }

    public static void MetricsIncludeHttp3PreviewCounters()
    {
        var metrics = new ProxyMetrics();
        metrics.QuicListenerStarted();
        metrics.Http3ConnectionAccepted();
        metrics.Http3RequestReceived();
        metrics.Http3ProxiedRequest();
        metrics.Http3GeneratedResponse();
        metrics.Http3StreamStarted();
        metrics.Http3StreamEnded();
        metrics.Http3StreamReset();
        metrics.Http3RequestRejected("method_unsupported");
        metrics.Http3ProtocolError("invalid_frame");
        metrics.SetActiveQuicListeners(1);
        var snapshot = metrics.Snapshot();

        AssertEx.Equal(1L, snapshot.QuicListenerStartSuccesses);
        AssertEx.Equal(1L, snapshot.Http3AcceptedConnections);
        AssertEx.Equal(1L, snapshot.Http3Requests);
        AssertEx.Equal(1L, snapshot.Http3ProxiedRequests);
        AssertEx.Equal(1L, snapshot.Http3GeneratedResponses);
        AssertEx.Equal(0L, snapshot.ActiveHttp3Streams);
        AssertEx.Equal(1L, snapshot.Http3StreamResets);
        AssertEx.Equal(1L, snapshot.Http3RejectedRequests["method_unsupported"]);
        AssertEx.Equal(1L, snapshot.Http3ProtocolErrors["invalid_frame"]);
        AssertEx.Equal(1L, snapshot.ActiveQuicListeners);
    }

    private static async Task<Http3ScenarioResult> RunHttp3GeneratedRouteScenarioAsync(
        string method,
        string target,
        string body,
        bool includeBodyData = false,
        bool dataBeforeHeaders = false,
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
        var response = await SendHttp3RequestAsync(port, method, target, timeout.Token, includeBodyData: includeBodyData, dataBeforeHeaders: dataBeforeHeaders);
        var metrics = host.Services.GetRequiredService<ProxyMetrics>().Snapshot();
        return new Http3ScenarioResult(temp, host, response.Headers, response.Body, metrics, "");
    }

    private static async Task<Http3ScenarioResult> RunHttp3ProxyRouteScenarioAsync(
        string method,
        string target,
        string upstreamResponse)
    {
        var temp = TemporaryDirectory.Create();
        var proxyPort = GetFreeTcpUdpPort();
        var upstreamPort = GetFreeTcpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3ProxySite(temp.Path, proxyPort, upstreamPort);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var upstreamTask = RunSingleResponseUpstreamAsync(upstreamPort, upstreamResponse, timeout.Token);
        var host = BuildProxyHost(temp.Path);
        await host.StartAsync(timeout.Token);

        var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
        await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);
        var response = await SendHttp3RequestAsync(proxyPort, method, target, timeout.Token);
        var upstreamRequest = await upstreamTask.WaitAsync(timeout.Token);
        var metrics = host.Services.GetRequiredService<ProxyMetrics>().Snapshot();
        return new Http3ScenarioResult(temp, host, response.Headers, response.Body, metrics, upstreamRequest);
    }

    private static async Task<Http3Response> SendHttp3RequestAsync(
        int port,
        string method,
        string target,
        CancellationToken cancellationToken,
        bool includeBodyData = false,
        bool dataBeforeHeaders = false)
    {
        await using var connection = await ConnectHttp3Async(port, cancellationToken);

        await using var stream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, cancellationToken);
        var headerBlock = Http3PreviewCodec.EncodeHeaderBlock(
        [
            new Http1HeaderField(":method", method),
            new Http1HeaderField(":scheme", "https"),
            new Http1HeaderField(":authority", "localhost"),
            new Http1HeaderField(":path", target)
        ]);
        using var request = new MemoryStream();
        if (dataBeforeHeaders)
        {
            Http3PreviewCodec.WriteFrame(request, Http3PreviewCodec.DataFrame, ReadOnlySpan<byte>.Empty);
        }

        Http3PreviewCodec.WriteFrame(request, Http3PreviewCodec.HeadersFrame, headerBlock);
        if (includeBodyData)
        {
            Http3PreviewCodec.WriteFrame(request, Http3PreviewCodec.DataFrame, "body"u8);
        }

        await stream.WriteAsync(request.ToArray(), completeWrites: true, cancellationToken);

        var responseBytes = await ReadToEndAsync(stream, cancellationToken);
        var offset = 0;
        IReadOnlyList<Http1HeaderField> headers = [];
        var body = "";
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
                body += Encoding.UTF8.GetString(payload.Span);
            }
        }

        await connection.CloseAsync(0, CancellationToken.None);
        return new Http3Response(headers, body);
    }

    private static ValueTask<QuicConnection> ConnectHttp3Async(
        int port,
        CancellationToken cancellationToken)
    {
        return QuicConnection.ConnectAsync(
            new QuicClientConnectionOptions
            {
                RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, port),
                ClientAuthenticationOptions = new SslClientAuthenticationOptions
                {
                    TargetHost = "localhost",
                    ApplicationProtocols = [Http3Alpn],
                    RemoteCertificateValidationCallback = static (_, _, _, _) => true
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
            var request = await ReadHttp1RequestHeadAsync(stream, cancellationToken);
            await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken);
            return request;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task<string> ReadHttp1RequestHeadAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[256];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return Encoding.ASCII.GetString(memory.ToArray());
            }

            memory.Write(buffer, 0, read);
            var text = Encoding.ASCII.GetString(memory.ToArray());
            if (text.Contains("\r\n\r\n", StringComparison.Ordinal))
            {
                return text;
            }
        }
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

    private static RuntimeListener PreviewListener(string protocols, bool experimental)
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
        var http3Enablement = protocols.Contains("http3", StringComparison.OrdinalIgnoreCase)
            ? "preview"
            : "disabled";
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
        string routeExtraJson = "")
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
        while (true)
        {
            var listener = runtimeState.Snapshot().Listeners.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(candidate.Kind, kind, StringComparison.OrdinalIgnoreCase)
                && candidate.State == state);
            if (listener is not null)
            {
                return listener;
            }

            await Task.Delay(25, cancellationToken);
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
