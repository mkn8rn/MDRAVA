using System.Net;
using System.Net.Sockets;
using System.Text;
using MDRAVA.API.Controllers;
using MDRAVA.API.Proxy.Caching;
using MDRAVA.API.Proxy.Configuration;
using MDRAVA.API.Proxy.Configuration.Loading;
using MDRAVA.API.Proxy.Configuration.Paths;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Configuration.Storage;
using MDRAVA.API.Proxy.Hosting;
using MDRAVA.API.Proxy.Protocol;
using MDRAVA.API.Proxy.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MDRAVA.Tests;

internal static class CacheTests
{
    public static async Task CachingDisabledByDefault()
    {
        using var temp = TemporaryDirectory.Create();
        ConfigurationTests.WriteSite(temp.Path, "default.json", 18080, 15000);

        var result = await CreateLoader(temp.Path).LoadAsync(CancellationToken.None);

        AssertEx.True(result.Succeeded, string.Join("; ", result.Errors));
        AssertEx.False(AssertEx.NotNull(result.Snapshot).Routes[0].Cache.Enabled);
    }

    public static async Task DisabledCacheKeepsExistingProxyBehavior()
    {
        var result = await RunTwoRequestProxyScenarioAsync(
            cacheEnabled: false,
            responseFactory: _ => "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 7\r\n\r\nproxied",
            firstRequest: "GET /resource HTTP/1.1\r\nHost: cache.test\r\nConnection: close\r\n\r\n",
            secondRequest: "GET /resource HTTP/1.1\r\nHost: cache.test\r\nConnection: close\r\n\r\n",
            expectedUpstreamRequests: 2);

        AssertEx.Equal(2, result.UpstreamRequests.Count);
        AssertEx.True(result.FirstResponse.EndsWith("proxied", StringComparison.Ordinal), result.FirstResponse);
        AssertEx.True(result.SecondResponse.EndsWith("proxied", StringComparison.Ordinal), result.SecondResponse);
    }

    public static void InvalidCachePolicyIsRejected()
    {
        var validation = new ProxyOptionsValidator().Validate(
            null,
            new ProxyOptions
            {
                Routes =
                [
                    new ProxyRouteOptions
                    {
                        Name = "bad-cache",
                        Host = "*",
                        PathPrefix = "/",
                        Cache = new ProxyCachePolicyOptions
                        {
                            Enabled = true,
                            MaxEntryBytes = -1,
                            Methods = ["POST"],
                            VaryByHeaders = ["bad header"]
                        },
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
            });

        AssertEx.True(validation.Failed);
        var failures = AssertEx.NotNull(validation.Failures);
        AssertEx.True(failures.Any(static failure => failure.Contains("MaxEntryBytes", StringComparison.Ordinal)));
        AssertEx.True(failures.Any(static failure => failure.Contains("Methods", StringComparison.Ordinal)));
        AssertEx.True(failures.Any(static failure => failure.Contains("VaryByHeaders", StringComparison.Ordinal)));
    }

    public static async Task EnabledGet200ResponseIsStoredAndServed()
    {
        var result = await RunTwoRequestProxyScenarioAsync(
            cacheEnabled: true,
            responseFactory: _ => "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 6\r\nCache-Control: max-age=60\r\n\r\ncached",
            firstRequest: "GET /resource HTTP/1.1\r\nHost: cache.test\r\nConnection: close\r\n\r\n",
            secondRequest: "GET /resource HTTP/1.1\r\nHost: cache.test\r\nConnection: close\r\n\r\n",
            expectedUpstreamRequests: 1);

        AssertEx.Equal(1, result.UpstreamRequests.Count);
        AssertEx.True(result.FirstResponse.EndsWith("cached", StringComparison.Ordinal), result.FirstResponse);
        AssertEx.True(result.SecondResponse.EndsWith("cached", StringComparison.Ordinal), result.SecondResponse);
        AssertEx.True(result.SecondResponse.Contains("Age:", StringComparison.OrdinalIgnoreCase), result.SecondResponse);
    }

    public static async Task HeadResponseReturnsHeadersWithoutBody()
    {
        var result = await RunTwoRequestProxyScenarioAsync(
            cacheEnabled: true,
            responseFactory: _ => "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 5\r\nX-Head: yes\r\n\r\n",
            firstRequest: "HEAD /head HTTP/1.1\r\nHost: cache.test\r\nConnection: close\r\n\r\n",
            secondRequest: "HEAD /head HTTP/1.1\r\nHost: cache.test\r\nConnection: close\r\n\r\n",
            expectedUpstreamRequests: 1);

        AssertEx.Equal(1, result.UpstreamRequests.Count);
        AssertEx.True(result.SecondResponse.Contains("X-Head: yes", StringComparison.OrdinalIgnoreCase), result.SecondResponse);
        AssertEx.True(result.SecondResponse.EndsWith("\r\n\r\n", StringComparison.Ordinal), result.SecondResponse);
    }

    public static void QueryStringIsPartOfCacheKey()
    {
        var cache = new ResponseCacheStore(new ManualTimeProvider());
        var route = Route(CachePolicy());
        var listener = Listener();
        var response = Response("200 OK", [new Http1HeaderField("Content-Type", "text/plain")]);
        cache.Store(route, listener, Request("GET", "/item?id=1", "cache.test"), "/item?id=1", response, response.Headers, Encoding.ASCII.GetBytes("one"));

        AssertEx.False(cache.TryGet(route, listener, Request("GET", "/item?id=2", "cache.test"), "/item?id=2", out _));
        AssertEx.True(cache.TryGet(route, listener, Request("GET", "/item?id=1", "cache.test"), "/item?id=1", out var cached));
        AssertEx.Equal("one", Encoding.ASCII.GetString(AssertEx.NotNull(cached).Body));
    }

    public static void HostAndVaryHeadersAffectCacheKey()
    {
        var cache = new ResponseCacheStore(new ManualTimeProvider());
        var route = Route(CachePolicy(varyByHeaders: ["X-Tenant"]));
        var listener = Listener();
        var response = Response("200 OK", []);
        cache.Store(
            route,
            listener,
            Request("GET", "/resource", "a.test", [new Http1HeaderField("X-Tenant", "one")]),
            "/resource",
            response,
            response.Headers,
            Encoding.ASCII.GetBytes("tenant-one"));

        AssertEx.False(cache.TryGet(route, listener, Request("GET", "/resource", "b.test", [new Http1HeaderField("X-Tenant", "one")]), "/resource", out _));
        AssertEx.False(cache.TryGet(route, listener, Request("GET", "/resource", "a.test", [new Http1HeaderField("X-Tenant", "two")]), "/resource", out _));
        AssertEx.True(cache.TryGet(route, listener, Request("GET", "/resource", "a.test", [new Http1HeaderField("x-tenant", "one")]), "/resource", out var cached));
        AssertEx.Equal("tenant-one", Encoding.ASCII.GetString(AssertEx.NotNull(cached).Body));
    }

    public static void AuthorizationRequestIsNotCachedByDefault()
    {
        var cache = new ResponseCacheStore(new ManualTimeProvider());
        var route = Route(CachePolicy());
        var listener = Listener();
        var request = Request("GET", "/private", "cache.test", [new Http1HeaderField("Authorization", "Bearer secret")]);
        var response = Response("200 OK", []);

        cache.Store(route, listener, request, "/private", response, response.Headers, Encoding.ASCII.GetBytes("private"));

        AssertEx.False(cache.TryGet(route, listener, request, "/private", out _));
        AssertEx.True(cache.Snapshot(null).StoreRejectionCount > 0);
    }

    public static void SetCookieResponseIsNotCachedByDefault()
    {
        AssertRejectedResponse([new Http1HeaderField("Set-Cookie", "id=1")]);
    }

    public static void NoStoreResponseIsNotCached()
    {
        AssertRejectedResponse([new Http1HeaderField("Cache-Control", "no-store")]);
    }

    public static void PrivateResponseIsNotCachedByDefault()
    {
        AssertRejectedResponse([new Http1HeaderField("Cache-Control", "private")]);
    }

    public static void MaxAgeControlsTtlAndExpiredEntryIsNotServed()
    {
        var clock = new ManualTimeProvider();
        var cache = new ResponseCacheStore(clock);
        var route = Route(CachePolicy(defaultTtlSeconds: 120));
        var listener = Listener();
        var response = Response("200 OK", [new Http1HeaderField("Cache-Control", "max-age=1")]);
        var request = Request("GET", "/ttl", "cache.test");

        cache.Store(route, listener, request, "/ttl", response, response.Headers, Encoding.ASCII.GetBytes("fresh"));

        AssertEx.True(cache.TryGet(route, listener, request, "/ttl", out _));
        clock.Advance(TimeSpan.FromSeconds(2));
        AssertEx.False(cache.TryGet(route, listener, request, "/ttl", out _));
    }

    public static async Task OversizedResponseIsStreamedButNotCached()
    {
        var body = "0123456789";
        var result = await RunTwoRequestProxyScenarioAsync(
            cacheEnabled: true,
            maxEntryBytes: 4,
            responseFactory: _ => $"HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: {body.Length}\r\n\r\n{body}",
            firstRequest: "GET /big HTTP/1.1\r\nHost: cache.test\r\nConnection: close\r\n\r\n",
            secondRequest: "GET /big HTTP/1.1\r\nHost: cache.test\r\nConnection: close\r\n\r\n",
            expectedUpstreamRequests: 2);

        AssertEx.Equal(2, result.UpstreamRequests.Count);
        AssertEx.True(result.FirstResponse.EndsWith(body, StringComparison.Ordinal), result.FirstResponse);
        AssertEx.True(result.SecondResponse.EndsWith(body, StringComparison.Ordinal), result.SecondResponse);
    }

    public static void HopByHopHeadersAndTransferEncodingAreNotStored()
    {
        var cache = new ResponseCacheStore(new ManualTimeProvider());
        var route = Route(CachePolicy());
        var listener = Listener();
        var request = Request("GET", "/headers", "cache.test");
        var response = Response(
            "200 OK",
            [
                new Http1HeaderField("Connection", "close"),
                new Http1HeaderField("Transfer-Encoding", "chunked"),
                new Http1HeaderField("X-Stored", "yes")
            ]);

        cache.Store(route, listener, request, "/headers", response, response.Headers, Encoding.ASCII.GetBytes("headers"));

        AssertEx.True(cache.TryGet(route, listener, request, "/headers", out var cached));
        var headers = AssertEx.NotNull(cached).Headers;
        AssertEx.False(headers.Any(static header => string.Equals(header.Name, "Connection", StringComparison.OrdinalIgnoreCase)));
        AssertEx.False(headers.Any(static header => string.Equals(header.Name, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase)));
        AssertEx.True(headers.Any(static header => string.Equals(header.Name, "X-Stored", StringComparison.OrdinalIgnoreCase)));
    }

    public static async Task CacheClearEndpointClearsEntries()
    {
        var cache = new ResponseCacheStore(new ManualTimeProvider());
        var store = CreateStoreWithRoute(Route(CachePolicy()));
        var route = store.Snapshot.Routes[0];
        var listener = store.Snapshot.Listeners[0];
        var request = Request("GET", "/clear", "cache.test");
        var response = Response("200 OK", []);
        cache.Store(route, listener, request, "/clear", response, response.Headers, Encoding.ASCII.GetBytes("clear"));
        var controller = new ProxyCacheController(cache, store);

        var status = controller.Clear();

        AssertEx.Equal(0, status.EntryCount);
        AssertEx.Equal("manual", status.LastClearReason);
        await Task.CompletedTask;
    }

    public static async Task CacheClearEndpointIsProtected()
    {
        var store = CreateStoreWithAdminAuthentication();
        var audit = new AdminAuditStore();
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/admin/proxy/cache/clear";
        context.Response.Body = new MemoryStream();
        context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        var middleware = new AdminAuthenticationMiddleware(
            _ => Task.CompletedTask,
            store,
            audit,
            NullLogger<AdminAuthenticationMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        AssertEx.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    public static async Task SuccessfulReloadClearsCache()
    {
        using var temp = TemporaryDirectory.Create();
        ConfigurationTests.WriteSite(temp.Path, "reload.json", 18080, 15000);
        var store = new ProxyConfigurationStore();
        var cache = new ResponseCacheStore(new ManualTimeProvider());
        var service = new ProxyConfigurationReloadService(CreateLoader(temp.Path), store, cache, NullLogger<ProxyConfigurationReloadService>.Instance);

        var first = await service.ReloadAsync(CancellationToken.None);
        AssertEx.True(first.Succeeded);
        SeedCache(cache, store.Snapshot.Routes[0], store.Snapshot.Listeners[0]);

        var second = await service.ReloadAsync(CancellationToken.None);

        AssertEx.True(second.Succeeded);
        AssertEx.Equal(0, cache.Snapshot(store.Snapshot).EntryCount);
        AssertEx.Equal("reload", cache.Snapshot(store.Snapshot).LastClearReason);
    }

    public static async Task FailedReloadDoesNotClearCache()
    {
        using var temp = TemporaryDirectory.Create();
        ConfigurationTests.WriteSite(temp.Path, "reload.json", 18080, 15000);
        var store = new ProxyConfigurationStore();
        var cache = new ResponseCacheStore(new ManualTimeProvider());
        var service = new ProxyConfigurationReloadService(CreateLoader(temp.Path), store, cache, NullLogger<ProxyConfigurationReloadService>.Instance);
        var first = await service.ReloadAsync(CancellationToken.None);
        AssertEx.True(first.Succeeded);
        SeedCache(cache, store.Snapshot.Routes[0], store.Snapshot.Listeners[0]);
        ConfigurationTests.WriteCustomSite(temp.Path, "broken.json", "{ nope");

        var second = await service.ReloadAsync(CancellationToken.None);

        AssertEx.False(second.Succeeded);
        AssertEx.Equal(1, cache.Snapshot(store.Snapshot).EntryCount);
    }

    private static void AssertRejectedResponse(IReadOnlyList<Http1HeaderField> responseHeaders)
    {
        var cache = new ResponseCacheStore(new ManualTimeProvider());
        var route = Route(CachePolicy());
        var listener = Listener();
        var request = Request("GET", "/reject", "cache.test");
        var response = Response("200 OK", responseHeaders);

        cache.Store(route, listener, request, "/reject", response, response.Headers, Encoding.ASCII.GetBytes("reject"));

        AssertEx.False(cache.TryGet(route, listener, request, "/reject", out _));
        AssertEx.True(cache.Snapshot(null).StoreRejectionCount > 0);
    }

    private static void SeedCache(ResponseCacheStore cache, RuntimeRoute route, RuntimeListener listener)
    {
        var request = Request("GET", "/seed", "cache.test");
        var response = Response("200 OK", []);
        cache.Store(route with { Cache = CachePolicy() }, listener, request, "/seed", response, response.Headers, Encoding.ASCII.GetBytes("seed"));
    }

    private static async Task<TwoRequestProxyResult> RunTwoRequestProxyScenarioAsync(
        bool cacheEnabled,
        Func<int, string> responseFactory,
        string firstRequest,
        string secondRequest,
        int expectedUpstreamRequests,
        long maxEntryBytes = 1024 * 1024)
    {
        var proxyPort = GetFreeTcpPort();
        var upstreamPort = GetFreeTcpPort();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var temp = TemporaryDirectory.Create();
        WriteCacheSite(temp.Path, proxyPort, upstreamPort, cacheEnabled, maxEntryBytes);
        var upstreamTask = RunCountingUpstreamAsync(upstreamPort, expectedUpstreamRequests, responseFactory, timeout.Token);

        using var host = BuildProxyHost(temp.Path);
        await host.StartAsync(timeout.Token);

        try
        {
            var first = await SendSingleRequestAsync(proxyPort, firstRequest, timeout.Token);
            var second = await SendSingleRequestAsync(proxyPort, secondRequest, timeout.Token);
            var requests = await upstreamTask.WaitAsync(timeout.Token);
            return new TwoRequestProxyResult(first, second, requests);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    private static async Task<IReadOnlyList<string>> RunCountingUpstreamAsync(
        int upstreamPort,
        int expectedRequests,
        Func<int, string> responseFactory,
        CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Loopback, upstreamPort);
        listener.Start();
        List<string> requests = [];

        try
        {
            while (requests.Count < expectedRequests)
            {
                using var client = await listener.AcceptTcpClientAsync(cancellationToken);
                await using var stream = client.GetStream();
                var request = await ReadRequestHeadAsync(stream, cancellationToken);
                requests.Add(request);
                var response = Encoding.ASCII.GetBytes(responseFactory(requests.Count));
                await stream.WriteAsync(response, cancellationToken);
            }

            return requests;
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

    private static void WriteCacheSite(
        string dataDirectory,
        int proxyPort,
        int upstreamPort,
        bool cacheEnabled,
        long maxEntryBytes)
    {
        ConfigurationTests.WriteCustomSite(
            dataDirectory,
            "cache.json",
            $$"""
            {
              "name": "cache",
              "listeners": [
                {
                  "name": "main",
                  "address": "127.0.0.1",
                  "port": {{proxyPort}}
                }
              ],
              "host": "*",
              "pathPrefix": "/",
              "cache": {
                "enabled": {{cacheEnabled.ToString().ToLowerInvariant()}},
                "maxEntryBytes": {{maxEntryBytes}},
                "maxTotalBytes": 1048576,
                "defaultTtlSeconds": 60,
                "respectOriginCacheControl": true
              },
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
        var provider = new MdravaDataDirectoryProvider(Options.Create(new MdravaDataDirectoryOptions
        {
            DataDirectory = dataDirectory
        }));

        return new ProxyConfigurationLoader(
            provider,
            new ProxyOptionsValidator(),
            new ProxyDataDirectoryBootstrapper(provider),
            new SiteConfigurationParser(),
            NullLogger<ProxyConfigurationLoader>.Instance);
    }

    private static ProxyConfigurationStore CreateStoreWithRoute(RuntimeRoute route)
    {
        var snapshot = new ProxyConfigurationSnapshot(
            1,
            DateTimeOffset.UtcNow,
            "tests",
            [],
            new ProxyConfigurationDiscovery(
                new ProxyFilesystemLayout("tests", "tests/config", "tests/config/sites", "tests/logs", "tests/certs", "tests/state", "tests/config/proxy.json"),
                [],
                [],
                []),
            new RuntimeAdminSecurityOptions([], false, false, null, "MDRAVA_ADMIN_TOKEN", "none", 100),
            new RuntimeAcmeOptions(false, true, "", [], false, "acme", 30, 720, 60, []),
            Timeouts(),
            new RuntimeConnectionLimits(100, 16, 1024),
            new RuntimeObservabilityOptions(true, 100),
            new RuntimeLimits(4096, 128, 240, 30, 32768, 128, 8192, 104857600, 8192, TimeSpan.FromSeconds(15)),
            new RuntimeForwardedHeadersOptions(true, []),
            new Dictionary<string, RuntimeCertificate>(StringComparer.OrdinalIgnoreCase),
            [Listener()],
            [route]);
        var store = new ProxyConfigurationStore();
        store.Replace(snapshot);
        return store;
    }

    private static ProxyConfigurationStore CreateStoreWithAdminAuthentication()
    {
        var snapshot = ProxyConfigurationMapper.ToRuntimeSnapshot(
            new ProxyOptions(),
            new ProxyOperationalOptions
            {
                Admin = new ProxyAdminOptions
                {
                    RequireAuthentication = true,
                    Token = "cache-admin-token"
                }
            },
            new Dictionary<string, RuntimeCertificate>(StringComparer.OrdinalIgnoreCase),
            1,
            DateTimeOffset.UtcNow,
            "tests",
            [],
            new ProxyConfigurationDiscovery(
                new ProxyFilesystemLayout("tests", "tests/config", "tests/config/sites", "tests/logs", "tests/certs", "tests/state", "tests/config/proxy.json"),
                [],
                [],
                []));
        var store = new ProxyConfigurationStore();
        store.Replace(snapshot);
        return store;
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
            new RuntimeRouteResolvedOptions(104857600, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), true));
    }

    private static RuntimeCachePolicy CachePolicy(
        int defaultTtlSeconds = 60,
        IReadOnlyList<string>? varyByHeaders = null)
    {
        return new RuntimeCachePolicy(
            true,
            1024 * 1024,
            16 * 1024 * 1024,
            TimeSpan.FromSeconds(defaultTtlSeconds),
            true,
            varyByHeaders ?? [],
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

    private static RuntimeTimeouts Timeouts()
    {
        return new RuntimeTimeouts(
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(10));
    }

    private static Http1RequestHead Request(
        string method,
        string target,
        string host,
        IReadOnlyList<Http1HeaderField>? headers = null)
    {
        var requestHeaders = new List<Http1HeaderField>
        {
            new("Host", host)
        };
        if (headers is not null)
        {
            requestHeaders.AddRange(headers);
        }

        var path = target.Split('?', 2)[0];
        return new Http1RequestHead(
            method,
            target,
            path,
            "HTTP/1.1",
            host,
            Http1RequestFraming.None,
            requestHeaders);
    }

    private static Http1ResponseHead Response(
        string status,
        IReadOnlyList<Http1HeaderField> headers)
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

    private sealed record TwoRequestProxyResult(
        string FirstResponse,
        string SecondResponse,
        IReadOnlyList<string> UpstreamRequests);

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void Advance(TimeSpan value)
        {
            _utcNow = _utcNow.Add(value);
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
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mdrava-cache-tests-{Guid.NewGuid():N}");
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
