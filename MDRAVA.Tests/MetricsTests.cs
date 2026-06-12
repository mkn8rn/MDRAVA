using System.Net;
using System.Net.Sockets;
using System.Text;
using MDRAVA.API.Controllers;
using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.Resilience;
using MDRAVA.INF.Configuration.Loading;
using MDRAVA.INF.Configuration.Paths;
using MDRAVA.INF.Proxy.Connections;
using MDRAVA.INF.Proxy.Health;
using MDRAVA.API.Proxy.Hosting;
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
        var audit = new AdminAuditStore(SilentLogPersistenceStore.Instance);
        var metrics = new ProxyMetrics();
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = RuntimeMetricsOptions.FixedAdminEndpointPath;
        context.Response.Body = new MemoryStream();
        context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        var middleware = ProxyAdminAuthenticationTestFactory.CreateMiddleware(
            _ => Task.CompletedTask,
            store,
            audit,
            metrics);

        await middleware.InvokeAsync(context);

        AssertEx.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        AssertEx.Equal(1L, metrics.Snapshot().AdminAuthFailures);
    }

    public static void MetricsEndpointReturnsPrometheusText()
    {
        using var fixture = MetricsFixture.Create();
        var store = CreateStore();
        var controller = new ProxyMetricsController(new ProxyMetricsAdministrationService(
            CreateExportProvider(store, fixture)));

        var content = (ContentResult)controller.Get();
        var text = AssertEx.NotNull(content.Content);

        AssertEx.Equal(PrometheusMetricsExporter.ContentType, content.ContentType);
        AssertEx.True(text.Contains("# HELP mdrava_requests_total", StringComparison.Ordinal));
        AssertEx.True(text.Contains("# TYPE mdrava_requests_total counter", StringComparison.Ordinal));
    }

    public static void MetricsExportInputSourceReadsActiveRuntimeFacts()
    {
        using var fixture = MetricsFixture.Create();
        var store = CreateStore();
        var source = CreateExportInputSource(
            new ProxyConfigurationMetricsExportConfigurationSource(store),
            store,
            fixture);

        var input = AssertEx.NotNull(source.ReadInput());

        AssertEx.Equal(0L, input.Metrics.TotalRequests);
        AssertEx.Equal(0L, input.Metrics.UpstreamFailures);
        AssertEx.Equal(0, input.DefaultEnabledHttp3ListenerCount);
        AssertEx.False(input.Http3RequestBodyStreamingEnabled);
        AssertEx.Equal(0, input.CacheStatus.Routes.Count);
    }

    public static void MetricsExportConfigurationMappersReadNarrowRuntimeFacts()
    {
        var metrics = new RuntimeMetricsOptions(
            true,
            RuntimeMetricsOptions.FixedAdminEndpointPath,
            true,
            false,
            true,
            false);
        var listener = Listener() with
        {
            Transport = RuntimeListenerTransport.Https,
            DefaultCertificateId = "metrics-cert",
            Protocols = RuntimeListenerProtocols.Http3
        };
        var upstream = new RuntimeUpstream(
            "metrics",
            "h3",
            "https",
            RuntimeUpstreamProtocol.Http3,
            "upstream.internal",
            443,
            1,
            RuntimeUpstreamTlsOptions.Default);
        var route = Route(RuntimeCachePolicy.Disabled) with
        {
            Upstreams = [upstream]
        };

        var labelOptions = ProxyMetricsExportLabelOptionsMapper.FromMetrics(metrics);
        var http3Facts = ProxyMetricsExportHttp3FactsMapper.FromRuntimeConfiguration([listener], [route]);

        AssertEx.False(labelOptions.IncludePerRouteLabels);
        AssertEx.True(labelOptions.IncludePerUpstreamLabels);
        AssertEx.Equal(1, http3Facts.DefaultEnabledListenerCount);
        AssertEx.True(http3Facts.RequestBodyStreamingEnabled);
        AssertEx.True(http3Facts.UpstreamMultiplexingConfigured);
    }

    public static void MetricsExportConfigurationMapperConsumesNamedFactsWithoutRuntimeConfiguration()
    {
        var labelOptions = new ProxyMetricsExportLabelOptions(
            IncludePerRouteLabels: false,
            IncludePerUpstreamLabels: true);
        var http3Facts = new ProxyMetricsExportHttp3Facts(
            DefaultEnabledListenerCount: 2,
            RequestBodyStreamingEnabled: true,
            UpstreamMultiplexingConfigured: false);

        var configuration = ProxyMetricsExportConfigurationMapper.FromSources(
            metricsEnabled: true,
            labelOptions,
            http3Facts);

        AssertEx.True(configuration.MetricsEnabled);
        AssertEx.False(configuration.LabelOptions.IncludePerRouteLabels);
        AssertEx.True(configuration.LabelOptions.IncludePerUpstreamLabels);
        AssertEx.Equal(2, configuration.Http3Facts.DefaultEnabledListenerCount);
        AssertEx.True(configuration.Http3Facts.RequestBodyStreamingEnabled);
        AssertEx.False(configuration.Http3Facts.UpstreamMultiplexingConfigured);
    }

    public static void MetricsEndpointReturnsNotFoundWhenMetricsDisabled()
    {
        using var fixture = MetricsFixture.Create();
        var store = CreateStore(new ProxyOperationalOptions
        {
            Metrics = new ProxyMetricsOptions
            {
                Enabled = false
            }
        });
        var controller = new ProxyMetricsController(new ProxyMetricsAdministrationService(
            CreateExportProvider(store, fixture)));

        var result = (NotFoundResult)controller.Get();

        AssertEx.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    public static void MetricsExportResultNamesAvailableAndUnavailableStates()
    {
        var notAvailable = ProxyMetricsExportResult.NotAvailable;
        var available = ProxyMetricsExportResult.Create("metrics text", PrometheusMetricsExporter.ContentType);

        AssertEx.False(notAvailable.Available);
        AssertEx.Equal(string.Empty, notAvailable.Content);
        AssertEx.Equal(string.Empty, notAvailable.ContentType);
        AssertEx.True(available.Available);
        AssertEx.Equal("metrics text", available.Content);
        AssertEx.Equal(PrometheusMetricsExporter.ContentType, available.ContentType);
    }

    public static void MetricsExportAvailabilityRequiresActiveEnabledConfig()
    {
        var missing = new ProxyMetricsExportAvailabilityService(
            new FixedMetricsExportAvailabilityReader(false, false)).GetAvailability();
        var disabled = new ProxyMetricsExportAvailabilityService(
            new FixedMetricsExportAvailabilityReader(true, false)).GetAvailability();
        var available = new ProxyMetricsExportAvailabilityService(
            new FixedMetricsExportAvailabilityReader(true, true)).GetAvailability();
        var fromState = ProxyMetricsExportAvailabilityResult.FromState(
            new ProxyMetricsExportAvailabilityState(
                HasActiveConfiguration: true,
                MetricsExportEnabled: true));

        AssertEx.False(missing.Available);
        AssertEx.False(missing.HasActiveConfiguration);
        AssertEx.False(missing.MetricsExportEnabled);
        AssertEx.False(disabled.Available);
        AssertEx.True(disabled.HasActiveConfiguration);
        AssertEx.False(disabled.MetricsExportEnabled);
        AssertEx.True(available.Available);
        AssertEx.True(fromState.Available);
        AssertEx.True(fromState.HasActiveConfiguration);
        AssertEx.True(fromState.MetricsExportEnabled);
    }

    public static void MetricsExportAvailabilityReaderNamesMissingConfiguration()
    {
        var reader = new ProxyMetricsExportAvailabilityReader(
            MissingMetricsExportConfigurationSource.Instance);

        var state = reader.Read();

        AssertEx.Equal(ProxyMetricsExportAvailabilityState.MissingConfiguration, state);
        AssertEx.False(state.HasActiveConfiguration);
        AssertEx.False(state.MetricsExportEnabled);
    }

    public static void MetricsExportAvailabilityStateMapsConfiguration()
    {
        var configuration = ProxyMetricsExportConfigurationMapper.FromSources(
            metricsEnabled: true,
            new ProxyMetricsExportLabelOptions(
                IncludePerRouteLabels: false,
                IncludePerUpstreamLabels: false),
            new ProxyMetricsExportHttp3Facts(
                DefaultEnabledListenerCount: 0,
                RequestBodyStreamingEnabled: false,
                UpstreamMultiplexingConfigured: false));

        var state = ProxyMetricsExportAvailabilityState.FromConfiguration(configuration);

        AssertEx.True(state.HasActiveConfiguration);
        AssertEx.True(state.MetricsExportEnabled);
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

        AssertEx.True(cache.Get(Scope(route, listener), request, "/cached") is ProxyCacheLookupResult.MissResult);
        cache.Store(Scope(route, listener), request, "/cached", response, response.Headers, Encoding.ASCII.GetBytes("cached"));
        AssertEx.True(cache.Get(Scope(route, listener), request, "/cached") is ProxyCacheLookupResult.HitResult);

        var text = fixture.Export(store.Snapshot);

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
            ActivatingProxyListenerReloadApplier.Instance,
            SilentProxyConfigurationReloadEventSink.Instance,
            TestHttp3PlatformSupport.SupportedSource);

        var first = await service.ReloadAsync(CancellationToken.None);
        AssertEx.True(first.Succeeded);
        ConfigurationTests.WriteCustomSite(temp.Path, "broken.json", "{ nope");
        var second = await service.ReloadAsync(CancellationToken.None);
        AssertEx.False(second.Succeeded);

        var text = fixture.Export(store.Snapshot);

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
        var failures = ProxyOperationalOptionsValidationRules.Validate(
            new ProxyOperationalOptions
            {
                Metrics = new ProxyMetricsOptions
                {
                    PublicMetricsEnabled = true
                }
            },
            static _ => null,
            new MDRAVA.INF.Configuration.ProxyAdminUrlPolicy(),
            new ProxyRelativeStoragePathPolicy(),
            new MDRAVA.INF.Configuration.ProxyUrlSyntaxPolicy(),
            new ProxyForwardedHeadersAddressPolicy());

        AssertEx.True(failures.Any(static failure => failure.Contains("PublicMetricsEnabled", StringComparison.Ordinal)), string.Join("; ", failures));
    }

    public static void MetricLabelsAreBoundedAndSanitized()
    {
        using var fixture = MetricsFixture.Create();
        var store = CreateStore();
        var rawSite = "site\nsecret";
        var rawRoute = "route\"with/slash/and/query?token=secret";
        fixture.Metrics.RequestCompleted(rawSite, rawRoute, "proxy\r\nbad", 200);

        var text = fixture.Export(store.Snapshot);

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
            var provider = host.Services.GetRequiredService<IProxyMetricsExportProvider>();
            return provider.Export().Content;
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
            var provider = host.Services.GetRequiredService<IProxyMetricsExportProvider>();
            return provider.Export().Content;
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
            new ProxyDataDirectoryBootstrapper(provider),
            new SiteConfigurationParser(),
            new MDRAVA.INF.Configuration.ProxyAdminUrlPolicy(),
            new ProxyEndpointAddressPolicy(),
            new ProxyRelativeStoragePathPolicy(),
            new MDRAVA.INF.Configuration.ProxyUrlSyntaxPolicy(),
            new ProxyForwardedHeadersAddressPolicy(),
            NullLogger<ProxyConfigurationLoader>.Instance,
            TimeProvider.System);
    }

    private static ProxyConfigurationStore CreateStore(ProxyOperationalOptions? operationalOptions = null)
    {
        var options = operationalOptions ?? new ProxyOperationalOptions();
        var store = new ProxyConfigurationStore();
        store.Replace(ProxyConfigurationRuntimeMapper.ToRuntimeSnapshot(
            new ProxyOptions(),
            options,
            ProxyAdminSecurityTokenPolicy.Resolve(options.Admin, static _ => null),
            new Dictionary<string, RuntimeCertificate>(StringComparer.OrdinalIgnoreCase),
            1,
            DateTimeOffset.UtcNow,
            "tests",
            [],
            Discovery()));
        return store;
    }

    private static ProxyMetricsExportProvider CreateExportProvider(
        ProxyConfigurationStore store,
        MetricsFixture fixture)
    {
        var configurationSource = new ProxyConfigurationMetricsExportConfigurationSource(store);

        return new ProxyMetricsExportProvider(
            CreateExportInputSource(configurationSource, store, fixture),
            fixture.Exporter,
            new ProxyMetricsExportAvailabilityService(
                new ProxyMetricsExportAvailabilityReader(configurationSource)));
    }

    private static ProxyMetricsExportInputSource CreateExportInputSource(
        IProxyMetricsExportConfigurationSource configurationSource,
        ProxyConfigurationStore store,
        MetricsFixture fixture)
    {
        return new ProxyMetricsExportInputSource(
            configurationSource,
            fixture.Metrics,
            new ProxyCacheStatusReader(
                new ProxyCacheStatusConfigurationSource(store),
                new ProxyCacheRuntimeStatusSource(fixture.Cache)),
            new ProxyStatusUpstreamHealthReader(store, fixture.Health),
            new ProxyAcmeCertificateLifecycleStatusSource(fixture.Acme));
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
        var operationalOptions = new ProxyOperationalOptions();
        var snapshot = ProxyConfigurationRuntimeMapper.ToRuntimeSnapshot(
            new ProxyOptions(),
            operationalOptions,
            ProxyAdminSecurityTokenPolicy.Resolve(operationalOptions.Admin, static _ => null),
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

    private static ProxyCacheRequestScope Scope(RuntimeRoute route, RuntimeListener listener)
    {
        return new ProxyCacheRequestScope(
            route.Name,
            route.Host,
            listener.Transport == RuntimeListenerTransport.Https ? "https" : "http",
            new ProxyCachePolicyFacts(
                route.Cache.Enabled,
                route.Cache.MaxEntryBytes,
                route.Cache.MaxTotalBytes,
                route.Cache.DefaultTtl,
                route.Cache.RespectOriginCacheControl,
                route.Cache.VaryByHeaders,
                route.Cache.CacheableStatusCodes,
                route.Cache.Methods));
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
            [new ProxyHeaderField("Host", host)]);
    }

    private static Http1ResponseHead Response(string status, IReadOnlyList<ProxyHeaderField> headers)
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
            UpstreamHealthStore health,
            AcmeCertificateStatusStore acme,
            PrometheusMetricsExporter exporter,
            UpstreamConnectionPool pool)
        {
            Metrics = metrics;
            Cache = cache;
            Health = health;
            Acme = acme;
            Exporter = exporter;
            _pool = pool;
        }

        public ProxyMetrics Metrics { get; }

        public ResponseCacheStore Cache { get; }

        public UpstreamHealthStore Health { get; }

        public AcmeCertificateStatusStore Acme { get; }

        public PrometheusMetricsExporter Exporter { get; }

        public string Export(ProxyConfigurationSnapshot snapshot)
        {
            return Exporter.Export(MetricsExportInput(
                snapshot,
                Metrics.Snapshot(),
                Cache.ReadStatusSnapshot(),
                Health.ReadUpstreams(ProxyUpstreamHealthSourceMapper.FromRoutes(snapshot.Routes)),
                Acme.Snapshot()));
        }

        private static ProxyMetricsExportInput MetricsExportInput(
            ProxyConfigurationSnapshot snapshot,
            ProxyMetricsSnapshot metrics,
            ProxyCacheRuntimeStatusSnapshot cacheRuntime,
            IReadOnlyList<ProxyUpstreamStatusResponse> upstreamHealth,
            IReadOnlyList<AcmeCertificateLifecycleStatus> acmeCertificates)
        {
            return ProxyMetricsExportInputMapper.FromSources(
                metrics,
                ProxyMetricsExportLabelOptionsMapper.FromMetrics(snapshot.Metrics),
                ProxyMetricsExportHttp3FactsMapper.FromRuntimeConfiguration(snapshot.Listeners, snapshot.Routes),
                ProxyCacheStatusReader.Project(
                    ProxyCacheStatusRouteSourceMapper.ToRouteSources(snapshot.Routes),
                    cacheRuntime),
                upstreamHealth,
                acmeCertificates);
        }

        public static MetricsFixture Create()
        {
            var metrics = new ProxyMetrics();
            var cache = new ResponseCacheStore(TimeProvider.System);
            var pool = new UpstreamConnectionPool(new UpstreamConnectionFactory(), metrics, TimeProvider.System);
            var circuit = new CircuitBreakerStore(metrics, TimeProvider.System);
            var health = new UpstreamHealthStore(metrics, pool, circuit);
            var acme = new AcmeCertificateStatusStore();
            return new MetricsFixture(
                metrics,
                cache,
                health,
                acme,
                new PrometheusMetricsExporter(),
                pool);
        }

        public void Dispose()
        {
            _pool.Dispose();
        }
    }

    private sealed class FixedMetricsExportAvailabilityReader : IProxyMetricsExportAvailabilityReader
    {
        private readonly ProxyMetricsExportAvailabilityState _state;

        public FixedMetricsExportAvailabilityReader(
            bool hasActiveConfiguration,
            bool metricsExportEnabled)
        {
            _state = new ProxyMetricsExportAvailabilityState(
                hasActiveConfiguration,
                metricsExportEnabled);
        }

        public ProxyMetricsExportAvailabilityState Read()
        {
            return _state;
        }
    }

    private sealed class MissingMetricsExportConfigurationSource : IProxyMetricsExportConfigurationSource
    {
        public static MissingMetricsExportConfigurationSource Instance { get; } = new();

        public ProxyMetricsExportConfiguration? ReadConfiguration()
        {
            return null;
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
