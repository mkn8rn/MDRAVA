using System.Net;
using MDRAVA.API.Controllers;
using MDRAVA.API.Models.Diagnostics;
using MDRAVA.API.Proxy.Caching;
using MDRAVA.API.Proxy.Configuration;
using MDRAVA.API.Proxy.Configuration.Loading;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Configuration.Storage;
using MDRAVA.API.Proxy.Diagnostics;
using MDRAVA.API.Proxy.Forwarding;
using MDRAVA.API.Proxy.Health;
using MDRAVA.API.Proxy.Hosting;
using MDRAVA.API.Proxy.Metrics;
using MDRAVA.API.Proxy.Protocol;
using MDRAVA.API.Proxy.Resilience;
using MDRAVA.API.Proxy.Routing;
using MDRAVA.API.Proxy.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace MDRAVA.Tests;

internal static class RouteDiagnosticsTests
{
    private const string AdminToken = "phase-27-admin-token";

    public static void DryRunMatchesSameRouteAsDataplaneMatcher()
    {
        var options = BaseOptions([
            ProxyRoute("api", "diag.test", "/api"),
            ProxyRoute("default", "diag.test", "/")
        ]);
        var service = CreateRouteService(options, out _, out _);
        var matcher = new SingleUpstreamRouteMatcher();
        var requestHead = Request("GET", "/api/users?id=1", "/api/users", "diag.test");

        var direct = matcher.Match(Snapshot(options), requestHead);
        var dryRun = service.Explain(new RouteMatchDryRunRequest("http", "diag.test", 8080, "GET", "/api/users", "?id=1", null, null, null));

        AssertEx.NotNull(direct);
        AssertEx.Equal(direct!.Route.Name, AssertEx.NotNull(dryRun.Route).Name);
        AssertEx.Equal("proxy", dryRun.EffectiveAction!);
    }

    public static void DryRunDoesNotPerformUpstreamIoOrMutateRetryCircuitOrCacheState()
    {
        var options = BaseOptions([
            ProxyRoute(
                "api",
                "diag.test",
                "/api",
                upstreamPort: 65500,
                cache: CachePolicy(),
                retry: RetryPolicy(),
                circuitBreaker: new ProxyCircuitBreakerOptions { Enabled = true })
        ]);
        var service = CreateRouteService(options, out var store, out var metrics);
        var cache = new ResponseCacheStore(TimeProvider.System);
        var beforeMetrics = metrics.Snapshot();
        var beforeCache = cache.Snapshot(store.Snapshot);

        var result = service.Explain(new RouteMatchDryRunRequest("http", "diag.test", 8080, "GET", "/api/users", "", null, null, null));

        var afterMetrics = metrics.Snapshot();
        var afterCache = cache.Snapshot(store.Snapshot);
        AssertEx.True(result.WouldProxy);
        AssertEx.Equal(0L, afterMetrics.RetryAttempts - beforeMetrics.RetryAttempts);
        AssertEx.Equal(0L, afterMetrics.CircuitRejections - beforeMetrics.CircuitRejections);
        AssertEx.Equal(beforeCache.HitCount, afterCache.HitCount);
        AssertEx.Equal(beforeCache.MissCount, afterCache.MissCount);
        AssertEx.Equal(1L, afterMetrics.RouteMatchDryRuns - beforeMetrics.RouteMatchDryRuns);
    }

    public static void DryRunReportsNoMatchReason()
    {
        var service = CreateRouteService(BaseOptions([ProxyRoute("api", "diag.test", "/api")]), out _, out _);

        var result = service.Explain(new RouteMatchDryRunRequest("http", "other.test", 8080, "GET", "/api", "", null, null, null));

        AssertEx.True(result.Succeeded);
        AssertEx.Equal("no_matching_route", result.NoMatchReason);
        AssertEx.Equal(false, result.WouldProxy);
    }

    public static void DryRunReportsPathRewriteResult()
    {
        var route = ProxyRoute(
            "api",
            "diag.test",
            "/public",
            pathRewrite: new ProxyPathRewriteOptions
            {
                StripPrefix = "/public"
            });
        var service = CreateRouteService(BaseOptions([route]), out _, out _);

        var result = service.Explain(new RouteMatchDryRunRequest("http", "diag.test", 8080, "GET", "/public/api/users", "id=1", null, null, null));

        AssertEx.Equal("/api/users?id=1", result.RewrittenTarget!);
    }

    public static void DryRunCanSelectHttp3ProtocolListener()
    {
        var options = new ProxyOptions
        {
            Listeners =
            [
                new ListenerOptions
                {
                    Name = "web",
                    Address = "127.0.0.1",
                    Port = 8443,
                    Transport = "https",
                    Protocols = "http1AndHttp2AndHttp3Preview",
                    ExperimentalHttp3 = true,
                    Http3Enablement = "preview",
                    DefaultCertificateId = "home-cert"
                }
            ],
            Routes = [ProxyRoute("api", "diag.test", "/api")]
        };
        var service = CreateRouteService(options, out _, out _);

        var result = service.Explain(new RouteMatchDryRunRequest(
            "https",
            "diag.test",
            8443,
            "GET",
            "/api",
            "",
            null,
            null,
            "web",
            "http3"));

        AssertEx.True(result.Succeeded);
        AssertEx.Equal("api", AssertEx.NotNull(result.Route).Name);
        var listener = AssertEx.NotNull(result.Listener);
        AssertEx.Equal("https", listener.Transport);
        AssertEx.True(listener.Protocols.Contains("Http3Preview", StringComparison.Ordinal));
    }

    public static void DryRunReportsGeneratedRouteActions()
    {
        var redirect = new ProxyRouteOptions
        {
            Name = "old",
            Host = "diag.test",
            PathPrefix = "/old",
            Action = "redirect",
            Redirect = new ProxyRedirectOptions { TargetPath = "/new", StatusCode = 308 }
        };
        var disabled = new ProxyRouteOptions
        {
            Name = "disabled",
            Host = "diag.test",
            PathPrefix = "/disabled",
            Action = "staticResponse",
            Maintenance = new ProxyMaintenanceOptions { Enabled = true }
        };
        var staticRoute = new ProxyRouteOptions
        {
            Name = "static",
            Host = "diag.test",
            PathPrefix = "/static",
            Action = "staticResponse",
            StaticResponse = new ProxyStaticResponseOptions
            {
                StatusCode = 410,
                Body = "Gone"
            }
        };
        var service = CreateRouteService(BaseOptions([redirect, disabled, staticRoute]), out _, out _);

        var redirectResult = service.Explain(new RouteMatchDryRunRequest("http", "diag.test", 8080, "GET", "/old", "", null, null, null));
        var maintenanceResult = service.Explain(new RouteMatchDryRunRequest("http", "diag.test", 8080, "GET", "/disabled", "", null, null, null));
        var staticResult = service.Explain(new RouteMatchDryRunRequest("http", "diag.test", 8080, "GET", "/static", "", null, null, null));

        AssertEx.Equal("redirect", redirectResult.EffectiveAction!);
        AssertEx.Equal(308, redirectResult.GeneratedStatusCode!.Value);
        AssertEx.Equal("maintenance", maintenanceResult.EffectiveAction!);
        AssertEx.Equal(503, maintenanceResult.GeneratedStatusCode!.Value);
        AssertEx.Equal("staticResponse", staticResult.EffectiveAction!);
        AssertEx.Equal(410, staticResult.GeneratedStatusCode!.Value);
    }

    public static void DryRunRedactsSensitiveHeaders()
    {
        var service = CreateRouteService(BaseOptions([ProxyRoute("private", "diag.test", "/private", cache: CachePolicy())]), out _, out _);

        var result = service.Explain(new RouteMatchDryRunRequest(
            "http",
            "diag.test",
            8080,
            "GET",
            "/private",
            "",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Authorization"] = "Bearer secret-token"
            },
            null,
            null));

        AssertEx.Equal("authorization", result.Cache.Reason);
        AssertEx.True(result.Findings.Any(static finding => finding.Code == "sensitive_header_redacted"));
        AssertEx.False(result.ToString()!.Contains("secret-token", StringComparison.Ordinal));
    }

    public static void LintDetectsRouteShadowingAndBroadCatchAll()
    {
        var service = CreateLintService(BaseOptions([
            ProxyRoute("catch-all", "*", "/"),
            ProxyRoute("api", "diag.test", "/api")
        ]));

        var result = service.LintActive();

        AssertFinding(result, "route_shadowed", "warning");
        AssertFinding(result, "broad_catch_all_before_specific", "warning");
    }

    public static void LintDetectsCanonicalRedirectLoop()
    {
        var route = new ProxyRouteOptions
        {
            Name = "loop",
            SiteName = "diag",
            Host = "diag.test",
            PathPrefix = "/",
            Action = "proxy",
            CanonicalHost = new ProxyCanonicalHostOptions
            {
                Enabled = true,
                TargetHost = "diag.test"
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
        };
        var service = CreateLintService(BaseOptions([route]));

        var result = service.LintActive();

        AssertFinding(result, "canonical_host_loop", "warning");
    }

    public static void LintDetectsHttpsRedirectWithoutHttpsListener()
    {
        var route = new ProxyRouteOptions
        {
            Name = "redirect",
            SiteName = "diag",
            Host = "diag.test",
            PathPrefix = "/",
            Action = "proxy",
            HttpsRedirect = new ProxyHttpsRedirectOptions { Enabled = true },
            Upstreams =
            [
                new UpstreamOptions
                {
                    Name = "local",
                    Address = "127.0.0.1",
                    Port = 5000
                }
            ]
        };
        var service = CreateLintService(BaseOptions([route]));

        var result = service.LintActive();

        AssertFinding(result, "https_redirect_without_https_listener", "warning");
    }

    public static void LintWarnsAboutUnsafeUpstreamTlsValidation()
    {
        var route = ProxyRoute("api", "diag.test", "/api", scheme: "https", upstreamTls: new UpstreamTlsOptions { ValidateCertificate = false });
        var service = CreateLintService(BaseOptions([route]));

        var result = service.LintActive();

        AssertFinding(result, "unsafe_upstream_tls_validation_disabled", "warning");
    }

    public static void LintReportsUpstreamHttp3FinalLimitations()
    {
        var route = ProxyRoute(
            "h3",
            "diag.test",
            "/h3",
            scheme: "https",
            upstreamProtocol: RuntimeUpstreamProtocol.Http3);
        var service = CreateLintService(BaseOptions([route]));

        var result = service.LintActive();

        AssertFinding(result, "upstream_http3_one_request_per_connection", "info");
    }

    public static void LintHandlesJsonAndYamlSubmittedConfigWithoutApplying()
    {
        var service = CreateLintService(BaseOptions([ProxyRoute("active", "active.test", "/")]), out var store);
        var json = SubmittedSiteJson("jsonsite", "json.test");
        var yaml = """
        name: yamlsite
        listeners:
          - name: web
            address: 127.0.0.1
            port: 8080
            transport: http
        host: yaml.test
        routes:
          - name: catch-all
            pathPrefix: /
            action: proxy
            upstreams:
              - name: local
                address: 127.0.0.1
                port: 5000
          - name: api
            pathPrefix: /api
            action: proxy
            upstreams:
              - name: local
                address: 127.0.0.1
                port: 5000
        """;

        var jsonResult = service.LintSubmitted(new ConfigLintRequest("json", json));
        var yamlResult = service.LintSubmitted(new ConfigLintRequest("yaml", yaml));

        AssertEx.True(jsonResult.Succeeded, string.Join("; ", jsonResult.Findings.Select(static finding => finding.Message)));
        AssertFinding(yamlResult, "route_shadowed", "warning");
        AssertEx.Equal("active", store.Snapshot.Routes[0].Name);
    }

    public static void LintOutputHasStableCodesAndSeverities()
    {
        var service = CreateLintService(BaseOptions([
            ProxyRoute("catch-all", "*", "/"),
            ProxyRoute("api", "diag.test", "/api")
        ]));

        var result = service.LintActive();

        var finding = result.Findings.First(static item => item.Code == "route_shadowed");
        AssertEx.Equal("warning", finding.Severity);
        AssertEx.True(result.Summary.Warning > 0);
    }

    public static async Task DiagnosticEndpointsRequireAdminAuth()
    {
        var store = CreateStore(BaseOptions([ProxyRoute("active", "active.test", "/")]));
        var audit = new AdminAuditStore();
        var metrics = new ProxyMetrics();
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/admin/proxy/routes/match";
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
    }

    public static void MetricsIncludeLintAndRouteDryRunCounters()
    {
        var options = BaseOptions([
            ProxyRoute("catch-all", "*", "/"),
            ProxyRoute("api", "diag.test", "/api")
        ]);
        var routeService = CreateRouteService(options, out var store, out var metrics);
        var lintService = CreateLintService(options, store, metrics);

        _ = routeService.Explain(new RouteMatchDryRunRequest("http", "diag.test", 8080, "GET", "/api", "", null, null, null));
        _ = lintService.LintActive();
        var snapshot = metrics.Snapshot();

        AssertEx.Equal(1L, snapshot.RouteMatchDryRuns);
        AssertEx.Equal(1L, snapshot.ConfigLintRuns);
        AssertEx.True(snapshot.ConfigLintFindings.Any(static finding => finding.Code == "route_shadowed"));
    }

    private static RouteMatchDiagnosticsService CreateRouteService(
        ProxyOptions options,
        out ProxyConfigurationStore store,
        out ProxyMetrics metrics)
    {
        store = CreateStore(options);
        metrics = new ProxyMetrics();
        return new RouteMatchDiagnosticsService(
            store,
            new SingleUpstreamRouteMatcher(),
            new ProxyRouteActionPolicy(),
            new PathRewritePolicy(),
            metrics,
            TimeProvider.System);
    }

    private static ConfigLintService CreateLintService(ProxyOptions options)
    {
        return CreateLintService(options, out _);
    }

    private static ConfigLintService CreateLintService(
        ProxyOptions options,
        out ProxyConfigurationStore store)
    {
        store = CreateStore(options);
        return CreateLintService(options, store, new ProxyMetrics());
    }

    private static ConfigLintService CreateLintService(
        ProxyOptions _,
        ProxyConfigurationStore store,
        ProxyMetrics metrics)
    {
        return new ConfigLintService(
            store,
            new ProxyRuntimeState(),
            new SiteConfigurationParser(),
            new ProxyOptionsValidator(),
            metrics,
            TimeProvider.System);
    }

    private static ProxyConfigurationStore CreateStore(ProxyOptions options)
    {
        var store = new ProxyConfigurationStore();
        store.Replace(Snapshot(options));
        return store;
    }

    private static ProxyConfigurationSnapshot Snapshot(ProxyOptions options)
    {
        return ProxyConfigurationMapper.ToRuntimeSnapshot(
            options,
            new ProxyOperationalOptions
            {
                Admin = new ProxyAdminOptions
                {
                    RequireAuthentication = true,
                    Token = AdminToken
                }
            },
            new Dictionary<string, RuntimeCertificate>(StringComparer.OrdinalIgnoreCase),
            1,
            DateTimeOffset.UnixEpoch,
            "test",
            ["site.json"],
            new ProxyConfigurationDiscovery(
                new ProxyFilesystemLayout("test", "test/config", "test/config/sites", "test/logs", "test/certs", "test/state", "test/config/proxy.json"),
                [],
                [],
                []));
    }

    private static ProxyOptions BaseOptions(IReadOnlyList<ProxyRouteOptions> routes)
    {
        return new ProxyOptions
        {
            Listeners =
            [
                new ListenerOptions
                {
                    Name = "web",
                    Address = "127.0.0.1",
                    Port = 8080,
                    Transport = "http"
                }
            ],
            Routes = routes.ToList()
        };
    }

    private static ProxyRouteOptions ProxyRoute(
        string name,
        string host,
        string pathPrefix,
        int upstreamPort = 5000,
        string scheme = "http",
        ProxyCachePolicyOptions? cache = null,
        ProxyRetryPolicyOptions? retry = null,
        UpstreamTlsOptions? upstreamTls = null,
        ProxyCircuitBreakerOptions? circuitBreaker = null,
        ProxyPathRewriteOptions? pathRewrite = null,
        string upstreamProtocol = RuntimeUpstreamProtocol.Http1)
    {
        return new ProxyRouteOptions
        {
            Name = name,
            SiteName = "diag",
            Host = host,
            PathPrefix = pathPrefix,
            Action = "proxy",
            Upstreams =
            [
                new UpstreamOptions
                {
                    Name = "local",
                    Scheme = scheme,
                    Protocol = upstreamProtocol,
                    Address = "127.0.0.1",
                    Port = upstreamPort,
                    UpstreamTls = upstreamTls ?? new UpstreamTlsOptions(),
                    CircuitBreaker = circuitBreaker ?? new ProxyCircuitBreakerOptions()
                }
            ],
            Cache = cache ?? new ProxyCachePolicyOptions(),
            Retry = retry ?? new ProxyRetryPolicyOptions(),
            PathRewrite = pathRewrite ?? new ProxyPathRewriteOptions()
        };
    }

    private static ProxyCachePolicyOptions CachePolicy()
    {
        return new ProxyCachePolicyOptions
        {
            Enabled = true,
            MaxEntryBytes = 4096,
            MaxTotalBytes = 8192,
            DefaultTtlSeconds = 60,
            Methods = ["GET", "HEAD"],
            CacheableStatusCodes = [200]
        };
    }

    private static ProxyRetryPolicyOptions RetryPolicy()
    {
        return new ProxyRetryPolicyOptions
        {
            Enabled = true,
            MaxAttempts = 2,
            RetryOnConnectFailure = true,
            RetryMethods = ["GET", "HEAD"]
        };
    }

    private static Http1RequestHead Request(string method, string target, string path, string host)
    {
        return new Http1RequestHead(method, target, path, "HTTP/1.1", host, Http1RequestFraming.None, []);
    }

    private static string SubmittedSiteJson(string name, string host)
    {
        return $$"""
        {
          "name": "{{name}}",
          "listeners": [
            {
              "name": "web",
              "address": "127.0.0.1",
              "port": 8080,
              "transport": "http"
            }
          ],
          "host": "{{host}}",
          "routes": [
            {
              "name": "app",
              "pathPrefix": "/",
              "action": "proxy",
              "upstreams": [
                {
                  "name": "local",
                  "address": "127.0.0.1",
                  "port": 5000
                }
              ]
            }
          ]
        }
        """;
    }

    private static void AssertFinding(ConfigLintResult result, string code, string severity)
    {
        AssertEx.True(
            result.Findings.Any(finding => finding.Code == code && finding.Severity == severity),
            string.Join("; ", result.Findings.Select(static finding => $"{finding.Severity}:{finding.Code}:{finding.Message}")));
    }
}
