using System.Net;
using System.Net.Sockets;
using System.Text;
using MDRAVA.API.Controllers;
using MDRAVA.API.Proxy.Acme;
using MDRAVA.API.Proxy.Caching;
using MDRAVA.API.Proxy.Configuration;
using MDRAVA.API.Proxy.Configuration.Loading;
using MDRAVA.INF.Configuration.Paths;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Configuration.Storage;
using MDRAVA.API.Proxy.Connections;
using MDRAVA.API.Proxy.Health;
using MDRAVA.API.Proxy.Hosting;
using MDRAVA.API.Proxy.Metrics;
using MDRAVA.API.Proxy.Protocol;
using MDRAVA.API.Proxy.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MDRAVA.Tests;

internal static class MetricsTests
{
    private const string AdminToken = "phase-17-admin-token";

    public static async Task MetricsEndpointIsProtectedByAdminAuth()
    {
        var store = CreateStoreWithAdminAuthentication();
        var audit = new AdminAuditStore();
        var metrics = new ProxyMetrics();
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = RuntimeMetricsOptions.FixedAdminEndpointPath;
        context.Response.Body = new MemoryStream();
        context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        var middleware = new AdminAuthenticationMiddleware(
            _ => Task.CompletedTask,
            store,
            audit,
            metrics,
            NullLogger<AdminAuthenticationMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        AssertEx.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        AssertEx.Equal(1L, metrics.Snapshot().AdminAuthFailures);
    }

    public static void MetricsEndpointReturnsPrometheusText()
    {
        using var fixture = MetricsFixture.Create();
        var store = CreateStore();
        var controller = new ProxyMetricsController(new ProxyMetricsAdministrationService(
            new ProxyMetricsExportProvider(store, fixture.Exporter)));

        var content = (ContentResult)controller.Get();
        var text = AssertEx.NotNull(content.Content);

        AssertEx.Equal(PrometheusMetricsExporter.ContentType, content.ContentType);
        AssertEx.True(text.Contains("# HELP mdrava_requests_total", StringComparison.Ordinal));
        AssertEx.True(text.Contains("# TYPE mdrava_requests_total counter", StringComparison.Ordinal));
    }

    public static async Task MetricsIncludeRequestCountersAfterProxiedRequest()
    {
        var text = await RunProxiedRequestAndExportAsync(
            "GET /metrics-check HTTP/1.1\r\nHost: metrics.test\r\nConnection: close\r\n\r\n");

        AssertEx.True(text.Contains("mdrava_requests_total 1", StringComparison.Ordinal), text);
        AssertEx.True(text.Contains("mdrava_route_requests_total", StringComparison.Ordinal), text);
        AssertEx.True(text.Contains("site=\"metrics\"", StringComparison.Ordinal), text);
        AssertEx.True(text.Contains("route=\"metrics\"", StringComparison.Ordinal), text);
        AssertEx.True(text.Contains("action=\"proxy\"", StringComparison.Ordinal), text);
        AssertEx.True(text.Contains("status_class=\"2xx\"", StringComparison.Ordinal), text);
    }

    public static void MetricsIncludeCacheCountersAfterCacheActivity()
    {
        using var fixture = MetricsFixture.Create();
        var cache = fixture.Cache;
        var store = CreateStoreWithRoute(Route(CachePolicy()));
        var route = store.Snapshot.Routes[0];
        var listener = store.Snapshot.Listeners[0];
        var request = Request("GET", "/cached", "cache.test");
        var response = Response("200 OK", []);

        AssertEx.False(cache.TryGet(route, listener, request, "/cached", out _));
        cache.Store(route, listener, request, "/cached", response, response.Headers, Encoding.ASCII.GetBytes("cached"));
        AssertEx.True(cache.TryGet(route, listener, request, "/cached", out _));

        var text = fixture.Exporter.Export(store.Snapshot);

        AssertEx.True(text.Contains("mdrava_cache_hits_total 1", StringComparison.Ordinal), text);
        AssertEx.True(text.Contains("mdrava_cache_misses_total 1", StringComparison.Ordinal), text);
        AssertEx.True(text.Contains("mdrava_cache_stores_total 1", StringComparison.Ordinal), text);
    }

    public static async Task MetricsIncludeReloadCounters()
    {
        using var temp = TemporaryDirectory.Create();
        ConfigurationTests.WriteSite(temp.Path, "reload.json", 18080, 15000);
        using var fixture = MetricsFixture.Create();
        var store = new ProxyConfigurationStore();
        var service = new ProxyConfigurationReloadService(
            CreateLoader(temp.Path),
            store,
            fixture.Cache,
            fixture.Metrics,
            NullLogger<ProxyConfigurationReloadService>.Instance);

        var first = await service.ReloadAsync(CancellationToken.None);
        AssertEx.True(first.Succeeded);
        ConfigurationTests.WriteCustomSite(temp.Path, "broken.json", "{ nope");
        var second = await service.ReloadAsync(CancellationToken.None);
        AssertEx.False(second.Succeeded);

        var text = fixture.Exporter.Export(store.Snapshot);

        AssertEx.True(text.Contains("mdrava_config_reloads_total{result=\"success\"} 1", StringComparison.Ordinal), text);
        AssertEx.True(text.Contains("mdrava_config_reloads_total{result=\"failure\"} 1", StringComparison.Ordinal), text);
    }

    public static async Task MetricsDoNotExposeRawRequestDetails()
    {
        var text = await RunProxiedRequestAndExportAsync(
            "GET /private/path?token=super-secret-token HTTP/1.1\r\nHost: metrics.test\r\nAuthorization: Bearer request-secret\r\nConnection: close\r\n\r\n");

        AssertEx.False(text.Contains("/private/path", StringComparison.Ordinal));
        AssertEx.False(text.Contains("super-secret-token", StringComparison.Ordinal));
        AssertEx.False(text.Contains("request-secret", StringComparison.Ordinal));
        AssertEx.False(text.Contains("metrics.test", StringComparison.OrdinalIgnoreCase));
        AssertEx.False(text.Contains("127.0.0.1", StringComparison.Ordinal));
    }

    public static async Task MetricsFailureMatrixDoesNotExposeAuthorizationCookieOrQuerySecrets()
    {
        var text = await RunFailedRequestAndExportAsync(
            "GET /missing/path?token=query-secret HTTP/1.1\r\nHost: other.test\r\nAuthorization: Bearer auth-secret\r\nCookie: session=cookie-secret\r\nConnection: close\r\n\r\n");

        AssertEx.True(text.Contains("mdrava_requests_total 1", StringComparison.Ordinal), text);
        AssertEx.True(text.Contains("status_class=\"4xx\"", StringComparison.Ordinal), text);
        AssertEx.False(text.Contains("/missing/path", StringComparison.Ordinal));
        AssertEx.False(text.Contains("query-secret", StringComparison.Ordinal));
        AssertEx.False(text.Contains("auth-secret", StringComparison.Ordinal));
        AssertEx.False(text.Contains("cookie-secret", StringComparison.Ordinal));
        AssertEx.False(text.Contains("other.test", StringComparison.OrdinalIgnoreCase));
    }

    public static void PublicMetricsExposureIsDisabledByDefault()
    {
        var snapshot = CreateStore().Snapshot;

        AssertEx.True(snapshot.Metrics.Enabled);
        AssertEx.Equal(RuntimeMetricsOptions.FixedAdminEndpointPath, snapshot.Metrics.EndpointPath);
        AssertEx.True(snapshot.Metrics.ProtectedByAdminAuth);
        AssertEx.False(snapshot.Metrics.PublicMetricsEnabled);
    }

    public static void InvalidMetricsConfigIsRejected()
    {
        var failures = ProxyOperationalOptionsValidator.Validate(new ProxyOperationalOptions
        {
            Metrics = new ProxyMetricsOptions
            {
                PublicMetricsEnabled = true
            }
        });

        AssertEx.True(failures.Any(static failure => failure.Contains("PublicMetricsEnabled", StringComparison.Ordinal)), string.Join("; ", failures));
    }

    public static void MetricLabelsAreBoundedAndSanitized()
    {
        using var fixture = MetricsFixture.Create();
        var store = CreateStore();
        var rawSite = "site\nsecret";
        var rawRoute = "route\"with/slash/and/query?token=secret";
        fixture.Metrics.RequestCompleted(rawSite, rawRoute, "proxy\r\nbad", 200);

        var text = fixture.Exporter.Export(store.Snapshot);

        AssertEx.False(text.Contains(rawSite, StringComparison.Ordinal));
        AssertEx.False(text.Contains(rawRoute, StringComparison.Ordinal));
        AssertEx.False(text.Contains("token=secret", StringComparison.Ordinal));
        AssertEx.True(text.Contains("site_secret", StringComparison.Ordinal), text);
        AssertEx.True(text.Contains("route_with_slash_and_query_token_secret", StringComparison.Ordinal), text);
    }

    private static async Task<string> RunProxiedRequestAndExportAsync(string request)
    {
        var proxyPort = GetFreeTcpPort();
        var upstreamPort = GetFreeTcpPort();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var temp = TemporaryDirectory.Create();
        WriteMetricsSite(temp.Path, proxyPort, upstreamPort);
        var upstreamTask = RunSingleUpstreamAsync(
            upstreamPort,
            "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 7\r\n\r\nmetrics",
            timeout.Token);

        using var host = BuildProxyHost(temp.Path);
        await host.StartAsync(timeout.Token);

        try
        {
            var response = await SendSingleRequestAsync(proxyPort, request, timeout.Token);
            AssertEx.True(response.Contains("200 OK", StringComparison.Ordinal), response);
            await upstreamTask.WaitAsync(timeout.Token);
            var store = host.Services.GetRequiredService<IProxyConfigurationStore>();
            var exporter = host.Services.GetRequiredService<PrometheusMetricsExporter>();
            return exporter.Export(store.Snapshot);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    private static async Task<string> RunFailedRequestAndExportAsync(string request)
    {
        var proxyPort = GetFreeTcpPort();
        var upstreamPort = GetFreeTcpPort();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var temp = TemporaryDirectory.Create();
        WriteMetricsSite(temp.Path, proxyPort, upstreamPort, host: "metrics.test");

        using var host = BuildProxyHost(temp.Path);
        await host.StartAsync(timeout.Token);

        try
        {
            var response = await SendSingleRequestAsync(proxyPort, request, timeout.Token);
            AssertEx.True(response.Contains("404 Not Found", StringComparison.Ordinal), response);
            var store = host.Services.GetRequiredService<IProxyConfigurationStore>();
            var exporter = host.Services.GetRequiredService<PrometheusMetricsExporter>();
            return exporter.Export(store.Snapshot);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    private static async Task<string> RunSingleUpstreamAsync(
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
            var request = await ReadRequestHeadAsync(stream, cancellationToken);
            await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken);
            return request;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task<string> SendSingleRequestAsync(
        int proxyPort,
        string request,
        CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, proxyPort, cancellationToken);
        await using var stream = client.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request), cancellationToken);
        return await ReadToEndAsync(stream, cancellationToken);
    }

    private static async Task<string> ReadToEndAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[1024];
        while (true)
        {
            var bytesRead = await stream.ReadAsync(chunk, cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            buffer.Write(chunk, 0, bytesRead);
        }

        return Encoding.ASCII.GetString(buffer.ToArray());
    }

    private static async Task<string> ReadRequestHeadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var total = 0;
        while (total < buffer.Length)
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(total, 1), cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            total += bytesRead;
            if (total >= 4
                && buffer[total - 4] == '\r'
                && buffer[total - 3] == '\n'
                && buffer[total - 2] == '\r'
                && buffer[total - 1] == '\n')
            {
                break;
            }
        }

        return Encoding.ASCII.GetString(buffer, 0, total);
    }

    private static void WriteMetricsSite(
        string dataDirectory,
        int proxyPort,
        int upstreamPort,
        string host = "*")
    {
        ConfigurationTests.WriteCustomSite(
            dataDirectory,
            "metrics.json",
            $$"""
            {
              "name": "metrics",
              "listeners": [
                {
                  "name": "main",
                  "address": "127.0.0.1",
                  "port": {{proxyPort}}
                }
              ],
              "host": "{{host}}",
              "pathPrefix": "/",
              "upstreams": [
                {
                  "name": "local-test",
                  "address": "127.0.0.1",
                  "port": {{upstreamPort}}
                }
              ]
            }
            """);
    }

    private static IHost BuildProxyHost(string dataDirectory)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(builder =>
            {
                builder.Sources.Clear();
                builder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Mdrava:DataDirectory"] = dataDirectory
                });
            })
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureServices((context, services) => services.AddProxyDataPlane(context.Configuration))
            .Build();
    }

    private static ProxyConfigurationLoader CreateLoader(string dataDirectory)
    {
        var provider = new MdravaDataDirectoryProvider(new MdravaDataDirectoryOptions
        {
            DataDirectory = dataDirectory
        });

        return new ProxyConfigurationLoader(
            provider,
            new ProxyOptionsValidator(),
            new ProxyDataDirectoryBootstrapper(provider),
            new SiteConfigurationParser(),
            NullLogger<ProxyConfigurationLoader>.Instance);
    }

    private static ProxyConfigurationStore CreateStore(ProxyOperationalOptions? operationalOptions = null)
    {
        var store = new ProxyConfigurationStore();
        store.Replace(ProxyConfigurationMapper.ToRuntimeSnapshot(
            new ProxyOptions(),
            operationalOptions ?? new ProxyOperationalOptions(),
            new Dictionary<string, RuntimeCertificate>(StringComparer.OrdinalIgnoreCase),
            1,
            DateTimeOffset.UtcNow,
            "tests",
            [],
            Discovery()));
        return store;
    }

    private static ProxyConfigurationStore CreateStoreWithAdminAuthentication()
    {
        return CreateStore(new ProxyOperationalOptions
        {
            Admin = new ProxyAdminOptions
            {
                RequireAuthentication = true,
                Token = AdminToken
            }
        });
    }

    private static ProxyConfigurationStore CreateStoreWithRoute(RuntimeRoute route)
    {
        var snapshot = ProxyConfigurationMapper.ToRuntimeSnapshot(
            new ProxyOptions(),
            new ProxyOperationalOptions(),
            new Dictionary<string, RuntimeCertificate>(StringComparer.OrdinalIgnoreCase),
            1,
            DateTimeOffset.UtcNow,
            "tests",
            [],
            Discovery()) with
        {
            Listeners = [Listener()],
            Routes = [route]
        };
        var store = new ProxyConfigurationStore();
        store.Replace(snapshot);
        return store;
    }

    private static ProxyConfigurationDiscovery Discovery()
    {
        return new ProxyConfigurationDiscovery(
            new ProxyFilesystemLayout("tests", "tests/config", "tests/config/sites", "tests/logs", "tests/certs", "tests/state", "tests/config/proxy.json"),
            [],
            [],
            []);
    }

    private static RuntimeRoute Route(RuntimeCachePolicy cache)
    {
        return new RuntimeRoute(
            "cache",
            "*",
            "/",
            RuntimeRouteAction.Proxy,
            "round-robin",
            new RuntimeHealthCheckOptions(false, "/health", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), 1, 1),
            [],
            new RuntimeHttpsRedirectPolicy(false, 308, null),
            new RuntimeCanonicalHostPolicy(false, "", 308),
            RuntimeHeaderPolicy.Empty,
            new RuntimePathRewritePolicy("", "", ""),
            new RuntimeRedirectPolicy(308, "", "", true),
            new RuntimeStaticResponse(200, "text/plain; charset=utf-8", ""),
            new RuntimeMaintenancePolicy(false, null, "text/plain; charset=utf-8", "Service Unavailable"),
            cache,
            new RuntimeRouteResolvedOptions(104857600, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), true))
        {
            SiteName = "cache"
        };
    }

    private static RuntimeCachePolicy CachePolicy()
    {
        return new RuntimeCachePolicy(
            true,
            1024 * 1024,
            16 * 1024 * 1024,
            TimeSpan.FromSeconds(60),
            true,
            [],
            [200],
            ["GET", "HEAD"]);
    }

    private static RuntimeListener Listener()
    {
        return new RuntimeListener(
            "main",
            "127.0.0.1",
            18080,
            true,
            RuntimeListenerTransport.Http,
            null,
            [],
            512,
            32768,
            32768,
            8192,
            8192);
    }

    private static Http1RequestHead Request(string method, string target, string host)
    {
        var path = target.Split('?', 2)[0];
        return new Http1RequestHead(
            method,
            target,
            path,
            "HTTP/1.1",
            host,
            Http1RequestFraming.None,
            [new Http1HeaderField("Host", host)]);
    }

    private static Http1ResponseHead Response(string status, IReadOnlyList<Http1HeaderField> headers)
    {
        var split = status.Split(' ', 2);
        return new Http1ResponseHead(
            "HTTP/1.1",
            int.Parse(split[0]),
            split.Length > 1 ? split[1] : "",
            Http1ResponseFraming.FromContentLength(0),
            headers);
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

    private sealed class MetricsFixture : IDisposable
    {
        private readonly UpstreamConnectionPool _pool;

        private MetricsFixture(
            ProxyMetrics metrics,
            ResponseCacheStore cache,
            PrometheusMetricsExporter exporter,
            UpstreamConnectionPool pool)
        {
            Metrics = metrics;
            Cache = cache;
            Exporter = exporter;
            _pool = pool;
        }

        public ProxyMetrics Metrics { get; }

        public ResponseCacheStore Cache { get; }

        public PrometheusMetricsExporter Exporter { get; }

        public static MetricsFixture Create()
        {
            var metrics = new ProxyMetrics();
            var cache = new ResponseCacheStore(TimeProvider.System);
            var pool = new UpstreamConnectionPool(new UpstreamConnectionFactory(), metrics);
            var health = new UpstreamHealthStore(metrics, pool);
            var acme = new AcmeCertificateStatusStore();
            return new MetricsFixture(
                metrics,
                cache,
                new PrometheusMetricsExporter(metrics, cache, health, acme),
                pool);
        }

        public void Dispose()
        {
            _pool.Dispose();
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
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mdrava-metrics-tests-{Guid.NewGuid():N}");
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
