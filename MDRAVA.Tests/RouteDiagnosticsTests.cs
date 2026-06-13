using System.Net;
using MDRAVA.API.Controllers;
using MDRAVA.INF.Configuration;
using MDRAVA.INF.Configuration.Loading;
using MDRAVA.INF.Proxy.Forwarding;
using MDRAVA.INF.Proxy.Health;
using MDRAVA.API.Proxy.Hosting;
using MDRAVA.API.Proxy.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

        var direct = matcher.Match(Snapshot(options).Routes, requestHead);
        var dryRun = service.Explain(new RouteMatchDryRunRequest("http", "diag.test", 8080, "GET", "/api/users", "?id=1", NoHeaders(), null, null));
        var matched = Matched(dryRun);

        AssertEx.NotNull(direct);
        AssertEx.Equal(direct!.Route.Name, matched.Route.Name);
        AssertEx.Equal("proxy", matched.EffectiveAction);
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
        var beforeCache = CacheStatus(cache, store.Snapshot);

        var result = service.Explain(new RouteMatchDryRunRequest("http", "diag.test", 8080, "GET", "/api/users", "", NoHeaders(), null, null));

        var afterMetrics = metrics.Snapshot();
        var afterCache = CacheStatus(cache, store.Snapshot);
        AssertEx.True(Matched(result).WouldProxy);
        AssertEx.Equal(0L, afterMetrics.RetryAttempts - beforeMetrics.RetryAttempts);
        AssertEx.Equal(0L, afterMetrics.CircuitRejections - beforeMetrics.CircuitRejections);
        AssertEx.Equal(beforeCache.HitCount, afterCache.HitCount);
        AssertEx.Equal(beforeCache.MissCount, afterCache.MissCount);
        AssertEx.Equal(1L, afterMetrics.RouteMatchDryRuns - beforeMetrics.RouteMatchDryRuns);
    }

    public static void DryRunReportsNoMatchReason()
    {
        var service = CreateRouteService(BaseOptions([ProxyRoute("api", "diag.test", "/api")]), out _, out _);

        var result = service.Explain(new RouteMatchDryRunRequest("http", "other.test", 8080, "GET", "/api", "", NoHeaders(), null, null));
        var noRoute = NoMatchingRoute(result);

        AssertEx.Equal("no_matching_route", noRoute.NoMatchReason);
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

        var result = service.Explain(new RouteMatchDryRunRequest("http", "diag.test", 8080, "GET", "/public/api/users", "id=1", NoHeaders(), null, null));

        AssertEx.Equal("/api/users?id=1", Matched(result).RewrittenTarget);
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
                    Protocols = "http1AndHttp2AndHttp3",
                    Http3Enablement = "default",
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
            NoHeaders(),
            null,
            "web",
            "http3"));

        var matched = Matched(result);

        AssertEx.Equal("api", matched.Route.Name);
        var listener = matched.Listener;
        AssertEx.Equal("https", listener.Transport);
        AssertEx.True(listener.Protocols.Contains("Http3", StringComparison.Ordinal));
    }

    public static void ListenerSelectorReadsListenersWithoutConfigurationSnapshot()
    {
        var listeners = new IProxyRouteDiagnosticsListener[]
        {
            RouteDiagnosticsListener(
                "plain",
                "http",
                8080,
                RuntimeListenerProtocols.Http1,
                false),
            RouteDiagnosticsListener(
                "secure",
                "https",
                8443,
                RuntimeListenerProtocols.Http1 | RuntimeListenerProtocols.Http2 | RuntimeListenerProtocols.Http3,
                true)
        };

        var selected = ProxyRouteDiagnosticsListenerSelector.Select(
            listeners,
            "secure",
            "https",
            8443,
            "http3");

        AssertEx.NotNull(selected);
        AssertEx.Equal("secure", selected!.Name);
        AssertEx.True(selected.Http3EnabledForTraffic);
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

        var redirectResult = service.Explain(new RouteMatchDryRunRequest("http", "diag.test", 8080, "GET", "/old", "", NoHeaders(), null, null));
        var maintenanceResult = service.Explain(new RouteMatchDryRunRequest("http", "diag.test", 8080, "GET", "/disabled", "", NoHeaders(), null, null));
        var staticResult = service.Explain(new RouteMatchDryRunRequest("http", "diag.test", 8080, "GET", "/static", "", NoHeaders(), null, null));

        var redirectMatch = Matched(redirectResult);
        var maintenanceMatch = Matched(maintenanceResult);
        var staticMatch = Matched(staticResult);

        AssertEx.Equal("redirect", redirectMatch.EffectiveAction);
        AssertEx.Equal(308, redirectMatch.GeneratedStatusCode!.Value);
        AssertEx.Equal("maintenance", maintenanceMatch.EffectiveAction);
        AssertEx.Equal(503, maintenanceMatch.GeneratedStatusCode!.Value);
        AssertEx.Equal("staticResponse", staticMatch.EffectiveAction);
        AssertEx.Equal(410, staticMatch.GeneratedStatusCode!.Value);
    }

    public static void DryRunPolicyExplainerUsesNamedPolicyOutcomes()
    {
        var route = RouteDiagnosticsRoute(
            "api",
            "diag.test",
            "/api",
            cacheEnabled: true,
            retryEnabled: true,
            circuitBreakerEnabled: true);
        var getRequest = new ProxyRouteDiagnosticsRequestHead(
            "GET",
            "/api",
            "/api",
            "HTTP/1.1",
            "diag.test",
            ProxyRouteDiagnosticsRequestFraming.None,
            []);
        var postRequest = new ProxyRouteDiagnosticsRequestHead(
            "POST",
            "/api",
            "/api",
            "HTTP/1.1",
            "diag.test",
            ProxyRouteDiagnosticsRequestFraming.None,
            []);

        var disabled = ProxyRouteDiagnosticsPolicyExplainer.Disabled("no_route");
        var cacheEligible = ProxyRouteDiagnosticsPolicyExplainer.ExplainCache(route, getRequest, wouldProxy: true);
        var retryBlocked = ProxyRouteDiagnosticsPolicyExplainer.ExplainRetry(route, postRequest, wouldProxy: true);
        var circuitBlocked = ProxyRouteDiagnosticsPolicyExplainer.ExplainCircuitBreaker(route, wouldProxy: false);

        AssertEx.Equal(RouteMatchDryRunPolicy.Disabled("no_route"), disabled);
        AssertEx.Equal(RouteMatchDryRunPolicy.EnabledAndApplies("eligible_before_origin_response"), cacheEligible);
        AssertEx.Equal(RouteMatchDryRunPolicy.EnabledButBlocked("method_not_retryable"), retryBlocked);
        AssertEx.Equal(RouteMatchDryRunPolicy.EnabledButBlocked("not_proxy_action"), circuitBlocked);
    }

    public static void RouteDiagnosticsActionDecisionNamesProxyAndGeneratedResponses()
    {
        var proxy = ProxyRouteDiagnosticsActionDecision.Proxy;
        var generated = ProxyRouteDiagnosticsActionDecision.GeneratedResponse(308);

        AssertEx.True(proxy.ShouldProxy);
        AssertEx.Equal<int?>(null, proxy.GeneratedStatusCode);
        AssertEx.False(generated.ShouldProxy);
        AssertEx.Equal(308, generated.GeneratedStatusCode!.Value);
    }

    public static void RouteDiagnosticsActionPolicyUsesSharedPolicyRedirects()
    {
        var route = RouteDiagnosticsRoute("api", "old.test", "/") with
        {
            HttpsRedirect = new ProxyRouteDiagnosticsHttpsRedirectPolicy(true, 307, 8443),
            CanonicalHost = new ProxyRouteDiagnosticsCanonicalHostPolicy(true, "canonical.test", 308)
        };
        var requestHead = new ProxyRouteDiagnosticsRequestHead(
            "GET",
            "/resource?id=1",
            "/resource",
            "HTTP/1.1",
            "old.test",
            ProxyRouteDiagnosticsRequestFraming.None,
            []);
        var listener = RouteDiagnosticsListener(
            "plain",
            "http",
            8080,
            RuntimeListenerProtocols.Http1,
            false);
        var policy = new ProxyRouteDiagnosticsActionPolicyAdapter();

        var decision = policy.Evaluate(route, requestHead, listener, isUpgradeRequest: false);

        AssertEx.False(decision.ShouldProxy);
        AssertEx.Equal(308, decision.GeneratedStatusCode!.Value);
    }

    public static void RouteDiagnosticsStatusNamesEnabledAvailability()
    {
        var status = RouteDiagnosticsStatus.Enabled;

        AssertEx.True(status.Available);
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
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Authorization"] = "Bearer secret-token"
            },
            null,
            null));

        AssertEx.Equal("authorization", result.Cache.Reason);
        AssertEx.True(result.Findings.Any(static finding => finding.Code == "sensitive_header_redacted"));
        AssertEx.False(result.ToString()!.Contains("secret-token", StringComparison.Ordinal));
    }

    public static void RouteDiagnosticsServiceShapesNoListenerResult()
    {
        var metrics = new FixedRouteDiagnosticsMetricsSink();
        var service = CreateBllRouteService(
            new FixedRouteDiagnosticsConfigurationSnapshot(
                [RouteDiagnosticsListener("secure", "https", 8443, RuntimeListenerProtocols.Http1AndHttp2, http3EnabledForTraffic: false)],
                [RouteDiagnosticsRoute("api", "diag.test", "/api")]),
            metricsSink: metrics);

        var result = service.Explain(new RouteMatchDryRunRequest("http", "diag.test", 8080, "GET", "/api", "", NoHeaders(), null, null));
        var noListener = NoMatchingListener(result);

        AssertEx.Equal("no_matching_listener", noListener.NoMatchReason);
        AssertEx.Equal("/api", noListener.OriginalTarget);
        AssertEx.Equal("no_route", result.Cache.Reason);
        AssertEx.True(result.Findings.Any(static finding => finding.Code == "no_matching_listener"));
        AssertEx.Equal("no_matching_listener", metrics.LastFailureReason!);
    }

    public static void RouteDiagnosticsServiceSelectsListenerBeforePolicyExplanation()
    {
        var actionPolicy = new FixedRouteDiagnosticsActionPolicy(ProxyRouteDiagnosticsActionDecision.Proxy);
        var metrics = new FixedRouteDiagnosticsMetricsSink();
        var service = CreateBllRouteService(
            new FixedRouteDiagnosticsConfigurationSnapshot(
                [
                    RouteDiagnosticsListener("web", "http", 8080, RuntimeListenerProtocols.Http1, http3EnabledForTraffic: false),
                    RouteDiagnosticsListener("secure", "https", 8443, RuntimeListenerProtocols.Http1AndHttp2AndHttp3, http3EnabledForTraffic: true)
                ],
                [RouteDiagnosticsRoute("api", "diag.test", "/api")]),
            actionPolicy: actionPolicy,
            metricsSink: metrics);

        var result = service.Explain(new RouteMatchDryRunRequest(
            "https",
            "diag.test",
            8443,
            "GET",
            "/api/users",
            "",
            NoHeaders(),
            null,
            "secure",
            "http3"));

        var matched = Matched(result);

        AssertEx.Equal("secure", matched.Listener.Name);
        AssertEx.Equal("secure", AssertEx.NotNull(actionPolicy.LastListener).Name);
        AssertEx.Equal("/api/users", AssertEx.NotNull(actionPolicy.LastRequestHead).Target);
        AssertEx.Equal(null, metrics.LastFailureReason);
    }

    public static void RouteDiagnosticsServiceRedactsSensitiveHeadersBeforeAdapters()
    {
        var actionPolicy = new FixedRouteDiagnosticsActionPolicy(ProxyRouteDiagnosticsActionDecision.Proxy);
        var service = CreateBllRouteService(
            new FixedRouteDiagnosticsConfigurationSnapshot(
                [RouteDiagnosticsListener("web", "http", 8080, RuntimeListenerProtocols.Http1, http3EnabledForTraffic: false)],
                [RouteDiagnosticsRoute("private", "diag.test", "/private", cacheEnabled: true)]),
            actionPolicy: actionPolicy);

        var result = service.Explain(new RouteMatchDryRunRequest(
            "http",
            "diag.test",
            8080,
            "GET",
            "/private",
            "",
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Authorization"] = "Bearer secret-token"
            },
            null,
            null));

        var requestHead = AssertEx.NotNull(actionPolicy.LastRequestHead);
        var authorization = requestHead.Headers.First(static header => string.Equals(header.Name, "Authorization", StringComparison.OrdinalIgnoreCase));
        AssertEx.Equal("redacted", authorization.Value);
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

    public static void LintDoesNotReportResolvedUpstreamHttp3PoolingLimitation()
    {
        var route = ProxyRoute(
            "h3",
            "diag.test",
            "/h3",
            scheme: "https",
            upstreamProtocol: RuntimeUpstreamProtocol.Http3);
        var service = CreateLintService(BaseOptions([route]));

        var result = service.LintActive();

        AssertNoFinding(result, "upstream_http3_one_request_per_connection");
    }

    public static void ConfigLintSnapshotMapperReadsRuntimeSourceWithoutConfigurationSnapshot()
    {
        var snapshot = Snapshot(BaseOptions([ProxyRoute("api", "diag.test", "/api")]));
        var source = ProxyConfigLintRuntimeConfigurationSourceMapper.FromConfiguration(snapshot);

        var lintSnapshot = ProxyConfigLintConfigurationSnapshotMapper.ToLintSnapshot(
            source,
            TestHttp3PlatformSupport.Supported);

        AssertEx.Equal(snapshot.SourceFiles.Count, lintSnapshot.SourceFiles.Count);
        AssertEx.Equal(snapshot.AdminSecurity.RequireAuthentication, lintSnapshot.AdminSecurity.RequireAuthentication);
        AssertEx.Equal(snapshot.Metrics.PublicMetricsEnabled, lintSnapshot.Metrics.PublicMetricsEnabled);
        AssertEx.Equal(snapshot.Listeners.Count, lintSnapshot.Listeners.Count);
        AssertEx.Equal(snapshot.Routes.Count, lintSnapshot.Routes.Count);
        AssertEx.Equal("api", lintSnapshot.Routes[0].Name);
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

        AssertAccepted(jsonResult, string.Join("; ", jsonResult.Findings.Select(static finding => finding.Message)));
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

    public static void ConfigLintServiceShapesActiveSourceFindings()
    {
        var metrics = new FixedConfigLintMetricsSink();
        var service = new ConfigLintService(
            new FixedConfigLintActiveConfigurationSource(LintSnapshot("active.json")),
            new FixedConfigLintSubmittedConfigurationSource(null),
            new FixedConfigLintRuntimeStateSource([]),
            metrics,
            new ProxyConfigLintSourceNameFormatter(),
            new ProxyAdminUrlPolicy(),
            TimeProvider.System);

        var result = service.LintActive();

        AssertAccepted(result);
        AssertFinding(result, "route_shadowed", "warning");
        AssertEx.Equal("active.json", result.Findings.First(static finding => finding.Code == "route_shadowed").Source);
        AssertEx.Equal(result.Summary, service.LastActiveStatus.LastActiveLintSummary);
        AssertEx.Equal(result.Findings.Count, metrics.LastFindings.Count);
    }

    public static void ConfigLintStatusNamesEmptyAndCompletedStates()
    {
        var lintedAtUtc = DateTimeOffset.UnixEpoch.AddHours(5);
        var summary = new ConfigLintSummary(Info: 1, Warning: 2, Error: 3);
        var empty = ConfigLintStatus.Empty;
        var completed = ConfigLintStatus.Completed(lintedAtUtc, summary);

        AssertEx.True(empty.Available);
        AssertEx.Equal<DateTimeOffset?>(null, empty.LastActiveLintAtUtc);
        AssertEx.Equal<ConfigLintSummary?>(null, empty.LastActiveLintSummary);
        AssertEx.True(completed.Available);
        AssertEx.Equal(lintedAtUtc, completed.LastActiveLintAtUtc!.Value);
        AssertEx.Equal(summary, completed.LastActiveLintSummary);
    }

    public static void ConfigLintSubmittedConfigurationResultNamesSourceOutcomes()
    {
        var snapshot = LintSnapshot("submitted.json");
        var validationError = ProxyConfigurationFileError.ForPath("lint-input", "Proxy:Routes:0:Name is required.");
        var loaded = ProxyConfigLintSubmittedConfigurationResult.Loaded(snapshot, [validationError]);
        var failed = ProxyConfigLintSubmittedConfigurationResult.Failed(
            ProxyConfigLintSubmittedConfigurationFailureKind.JsonParseError,
            "bad json");
        var empty = ProxyConfigLintSubmittedConfigurationResult.Empty();

        AssertEx.True(loaded is ProxyConfigLintSubmittedConfigurationResult.LoadedResult);
        var loadedResult = (ProxyConfigLintSubmittedConfigurationResult.LoadedResult)loaded;
        AssertEx.Equal(snapshot, loadedResult.Snapshot);
        AssertEx.Equal(validationError, loadedResult.ValidationErrors[0]);
        AssertEx.True(failed is ProxyConfigLintSubmittedConfigurationResult.FailedResult);
        var failure = ((ProxyConfigLintSubmittedConfigurationResult.FailedResult)failed).Failure;
        AssertEx.Equal(ProxyConfigLintSubmittedConfigurationFailureKind.JsonParseError, failure.Kind);
        AssertEx.Equal("bad json", failure.Message);
        AssertEx.True(empty is ProxyConfigLintSubmittedConfigurationResult.EmptyResult);
    }

    public static void ConfigLintServiceShapesSubmittedSourceFindings()
    {
        var source = new FixedConfigLintSubmittedConfigurationSource(
            ProxyConfigLintSubmittedConfigurationResult.Loaded(
                LintSnapshot("submitted.json"),
                [ProxyConfigurationFileError.ForPath("lint-input", "Proxy:Routes:0:Name is required.")]));
        var service = new ConfigLintService(
            new FixedConfigLintActiveConfigurationSource(null),
            source,
            new FixedConfigLintRuntimeStateSource([]),
            new FixedConfigLintMetricsSink(),
            new ProxyConfigLintSourceNameFormatter(),
            new ProxyAdminUrlPolicy(),
            TimeProvider.System);

        var result = service.LintSubmitted(new ConfigLintRequest("yml", "submitted"));

        AssertRejected(result);
        AssertEx.Equal(ProxyConfigurationNormalizeFormat.Yaml, source.LastFormat);
        AssertFinding(result, "validation_error", "error");
        AssertFinding(result, "route_shadowed", "warning");
        AssertEx.Equal("lint-input", result.Findings.First(static finding => finding.Code == "route_shadowed").Source);
        AssertEx.Equal("lint-input", result.ValidationErrors[0].Path);
    }

    public static void ConfigLintRejectsMissingSubmittedRequestBody()
    {
        var service = CreateLintService(BaseOptions([ProxyRoute("active", "active.test", "/")]));

        var result = service.LintSubmitted(null);

        AssertRejected(result);
        AssertFinding(result, "missing_request", "error");
        AssertEx.True(result.Findings[0].Message.Contains("request body is required", StringComparison.Ordinal));
    }

    public static void ConfigLintSubmittedRequestReaderAcceptsYamlInput()
    {
        var decision = ConfigLintSubmittedRequestReader.Read(new ConfigLintRequest("yml", "submitted"));

        AssertEx.True(decision is ConfigLintSubmittedRequestDecision.AcceptedDecision);
        var accepted = (ConfigLintSubmittedRequestDecision.AcceptedDecision)decision;
        AssertEx.Equal("submitted", accepted.Input.Text);
        AssertEx.Equal(ProxyConfigurationNormalizeFormat.Yaml, accepted.Input.Format);
    }

    public static void ConfigLintSubmittedRequestReaderRejectsMissingRequest()
    {
        var decision = ConfigLintSubmittedRequestReader.Read(null);

        AssertEx.True(decision is ConfigLintSubmittedRequestDecision.RejectedDecision);
        var rejected = (ConfigLintSubmittedRequestDecision.RejectedDecision)decision;
        AssertEx.Equal("missing_request", rejected.Failure.Code);
        AssertEx.Equal("error", rejected.Failure.Severity);
    }

    public static void ConfigLintControllerRejectsMissingSubmittedRequestBody()
    {
        var service = CreateLintService(BaseOptions([ProxyRoute("active", "active.test", "/")]));
        var controller = new ProxyConfigLintController(
            new ProxyConfigLintAdministrationService(service));

        var actionResult = controller.Submitted(null);

        var badRequest = (BadRequestObjectResult)AssertEx.NotNull(actionResult.Result);
        var result = (ConfigLintResponse)AssertEx.NotNull(badRequest.Value);
        AssertEx.False(result.Succeeded);
        AssertFinding(result, "missing_request", "error");
        AssertEx.True(result.Findings[0].Message.Contains("request body is required", StringComparison.Ordinal));
    }

    public static void ConfigLintControllerRejectsIncompleteSubmittedRequestFields()
    {
        var service = CreateLintService(BaseOptions([ProxyRoute("active", "active.test", "/")]));
        var controller = new ProxyConfigLintController(
            new ProxyConfigLintAdministrationService(service));

        var missingFormat = controller.Submitted(new ProxyConfigLintSubmissionRequest(null, "submitted"));
        var missingText = controller.Submitted(new ProxyConfigLintSubmissionRequest("json", null));

        var formatResponse = (BadRequestObjectResult)AssertEx.NotNull(missingFormat.Result);
        var formatResult = (ConfigLintResponse)AssertEx.NotNull(formatResponse.Value);
        AssertFinding(formatResult, "invalid_format", "error");
        var textResponse = (BadRequestObjectResult)AssertEx.NotNull(missingText.Result);
        var textResult = (ConfigLintResponse)AssertEx.NotNull(textResponse.Value);
        AssertFinding(textResult, "empty_config", "error");
        AssertEx.True(textResult.Findings[0].Message.Contains("config text is required", StringComparison.Ordinal));
    }

    public static void RouteDiagnosticsControllerRejectsMissingMatchRequestBody()
    {
        var service = CreateRouteService(BaseOptions([ProxyRoute("active", "active.test", "/")]), out _, out _);
        var controller = new ProxyRouteDiagnosticsController(
            new ProxyRouteDiagnosticsAdministrationService(service));

        var actionResult = controller.Match(null);

        var badRequest = (BadRequestObjectResult)AssertEx.NotNull(actionResult.Result);
        var result = (RouteMatchDryRunResponse)AssertEx.NotNull(badRequest.Value);
        AssertEx.False(result.Succeeded);
        AssertEx.True(result.Findings.Any(static finding =>
            finding.Code == "missing_request" && finding.Severity == "error"));
    }

    public static void RouteDiagnosticsRequestReaderAcceptsNormalizedInput()
    {
        var evaluatedAt = new DateTimeOffset(2026, 6, 13, 10, 0, 0, TimeSpan.Zero);
        var request = new RouteMatchDryRunRequest(
            "HTTPS",
            " diag.test ",
            8443,
            "post",
            "/api",
            "id=1",
            new Dictionary<string, string?>
            {
                ["Authorization"] = "secret",
                ["X-Test"] = "value"
            },
            "127.0.0.1",
            "tls",
            "HTTP3");

        var decision = ProxyRouteDiagnosticsRequestReader.Read(
            request,
            evaluatedAt,
            new MDRAVA.INF.Proxy.RuntimeGuards.ProxyClientAddressSyntaxPolicy());

        AssertEx.True(decision is ProxyRouteDiagnosticsRequestDecision.AcceptedDecision);
        var accepted = (ProxyRouteDiagnosticsRequestDecision.AcceptedDecision)decision;
        AssertEx.Equal("https", accepted.Input.Scheme);
        AssertEx.Equal("http3", accepted.Input.Protocol!);
        AssertEx.Equal("/api?id=1", accepted.Input.Target);
        AssertEx.Equal("diag.test", accepted.Input.RequestHead.Host);
        AssertEx.True(accepted.Input.Findings.Any(static finding => finding.Code == "sensitive_header_redacted"));
    }

    public static void RouteDiagnosticsRequestReaderRejectsInvalidScheme()
    {
        var evaluatedAt = new DateTimeOffset(2026, 6, 13, 10, 0, 0, TimeSpan.Zero);
        var request = new RouteMatchDryRunRequest("ftp", "diag.test", null, "GET", "/", "", NoHeaders(), null, null);

        var decision = ProxyRouteDiagnosticsRequestReader.Read(
            request,
            evaluatedAt,
            new MDRAVA.INF.Proxy.RuntimeGuards.ProxyClientAddressSyntaxPolicy());

        AssertEx.True(decision is ProxyRouteDiagnosticsRequestDecision.RejectedDecision);
        var rejected = (ProxyRouteDiagnosticsRequestDecision.RejectedDecision)decision;
        AssertEx.Equal("invalid_scheme", rejected.Failure.FailureReason);
    }

    public static void RouteDiagnosticsControllerMapsMissingHeadersToEmptyInput()
    {
        var service = CreateRouteService(BaseOptions([ProxyRoute("active", "active.test", "/")]), out _, out _);
        var controller = new ProxyRouteDiagnosticsController(
            new ProxyRouteDiagnosticsAdministrationService(service));

        var actionResult = controller.Match(new ProxyRouteMatchDryRunRequest(
            "http",
            "active.test",
            8080,
            "GET",
            "/",
            "",
            null,
            null,
            null));

        var ok = (OkObjectResult)AssertEx.NotNull(actionResult.Result);
        var result = (RouteMatchDryRunResponse)AssertEx.NotNull(ok.Value);
        AssertEx.True(result.Succeeded);
        AssertEx.Equal("active", AssertEx.NotNull(result.Route).Name);
    }

    public static async Task DiagnosticEndpointsRequireAdminAuth()
    {
        var store = CreateStore(BaseOptions([ProxyRoute("active", "active.test", "/")]));
        var audit = new AdminAuditStore(SilentLogPersistenceStore.Instance);
        var metrics = new ProxyMetrics();
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/admin/proxy/routes/match";
        context.Response.Body = new MemoryStream();
        context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        var middleware = ProxyAdminAuthenticationTestFactory.CreateMiddleware(
            _ => Task.CompletedTask,
            store,
            audit,
            metrics);

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

        _ = routeService.Explain(new RouteMatchDryRunRequest("http", "diag.test", 8080, "GET", "/api", "", NoHeaders(), null, null));
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
            new ProxyRouteDiagnosticsConfigurationSource(store),
            new ProxyRouteDiagnosticsMatcher(),
            new ProxyRouteDiagnosticsActionPolicyAdapter(),
            new ProxyRouteDiagnosticsPathRewritePolicyAdapter(),
            metrics,
            new MDRAVA.INF.Proxy.RuntimeGuards.ProxyClientAddressSyntaxPolicy(),
            TimeProvider.System);
    }

    private static ProxyCacheStatusResponse CacheStatus(
        ResponseCacheStore cache,
        ProxyConfigurationSnapshot? configuration)
    {
        return ProxyCacheStatusReader.Project(
            configuration is null
                ? []
                : ProxyCacheStatusRouteSourceMapper.ToRouteSources(configuration.Routes),
            cache.ReadStatusSnapshot());
    }

    private static RouteMatchDiagnosticsService CreateBllRouteService(
        IProxyRouteDiagnosticsConfigurationSnapshot? snapshot,
        FixedRouteDiagnosticsMatcher? matcher = null,
        FixedRouteDiagnosticsActionPolicy? actionPolicy = null,
        FixedRouteDiagnosticsPathRewritePolicy? pathRewritePolicy = null,
        FixedRouteDiagnosticsMetricsSink? metricsSink = null)
    {
        return new RouteMatchDiagnosticsService(
            new FixedRouteDiagnosticsConfigurationSource(snapshot),
            matcher ?? new FixedRouteDiagnosticsMatcher(),
            actionPolicy ?? new FixedRouteDiagnosticsActionPolicy(ProxyRouteDiagnosticsActionDecision.Proxy),
            pathRewritePolicy ?? new FixedRouteDiagnosticsPathRewritePolicy(),
            metricsSink ?? new FixedRouteDiagnosticsMetricsSink(),
            new MDRAVA.INF.Proxy.RuntimeGuards.ProxyClientAddressSyntaxPolicy(),
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
            new ProxyConfigLintActiveConfigurationSource(
                store,
                TestHttp3PlatformSupport.SupportedSource),
            new ProxyConfigLintSubmittedConfigurationSource(
                new SiteConfigurationParser(),
                TestHttp3PlatformSupport.SupportedSource,
                new ProxyEndpointAddressPolicy(),
                new ProxyUrlSyntaxPolicy()),
            new ProxyConfigLintRuntimeStateSource(new ProxyRuntimeState(TimeProvider.System)),
            metrics,
            new ProxyConfigLintSourceNameFormatter(),
            new ProxyAdminUrlPolicy(),
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
        var operationalOptions = new ProxyOperationalOptions
        {
            Admin = new ProxyAdminOptions
            {
                RequireAuthentication = true,
                Token = AdminToken
            }
        };
        return ProxyConfigurationRuntimeMapper.ToRuntimeSnapshot(
            options,
            operationalOptions,
            ProxyAdminSecurityTokenPolicy.Resolve(operationalOptions.Admin, static _ => null),
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

    private static IReadOnlyDictionary<string, string?> NoHeaders()
    {
        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
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

    private static ProxyConfigLintConfigurationSnapshot LintSnapshot(string sourceFile)
    {
        return new ProxyConfigLintConfigurationSnapshot(
            [sourceFile],
            new ProxyConfigLintAdminSecurity([AdminBindPolicy.DefaultAdminUrl], true),
            new ProxyConfigLintMetricsOptions(false),
            true,
            [
                new ProxyConfigLintListener(
                    "web",
                    "127.0.0.1",
                    8080,
                    true,
                    "Http",
                    false,
                    false,
                    "not_configured",
                    "disabled",
                    false,
                    null)
            ],
            [
                LintRoute("catch-all", "*", "/"),
                LintRoute("api", "diag.test", "/api")
            ]);
    }

    private static ProxyConfigLintRoute LintRoute(string name, string host, string pathPrefix)
    {
        return new ProxyConfigLintRoute(
            name,
            "diag",
            host,
            pathPrefix,
            "Proxy",
            false,
            false,
            "",
            false,
            [],
            false,
            ["GET", "HEAD"],
            false,
            [
                new ProxyConfigLintUpstream(
                    "local",
                    "http",
                    RuntimeUpstreamProtocol.Http1,
                    true,
                    false)
            ],
            "");
    }

    private sealed class FixedConfigLintActiveConfigurationSource
        : IProxyConfigLintActiveConfigurationSource
    {
        private readonly ProxyConfigLintConfigurationSnapshot? _snapshot;

        public FixedConfigLintActiveConfigurationSource(ProxyConfigLintConfigurationSnapshot? snapshot)
        {
            _snapshot = snapshot;
        }

        public ProxyConfigLintActiveConfigurationReadResult Read()
        {
            return _snapshot is null
                ? ProxyConfigLintActiveConfigurationReadResult.MissingConfiguration
                : ProxyConfigLintActiveConfigurationReadResult.Available(_snapshot);
        }
    }

    private sealed class FixedConfigLintSubmittedConfigurationSource
        : IProxyConfigLintSubmittedConfigurationSource
    {
        private readonly ProxyConfigLintSubmittedConfigurationResult? _result;

        public FixedConfigLintSubmittedConfigurationSource(ProxyConfigLintSubmittedConfigurationResult? result)
        {
            _result = result;
        }

        public ProxyConfigurationNormalizeFormat? LastFormat { get; private set; }

        public ProxyConfigLintSubmittedConfigurationResult Read(
            string text,
            ProxyConfigurationNormalizeFormat format,
            DateTimeOffset loadedAtUtc)
        {
            _ = text;
            _ = loadedAtUtc;
            LastFormat = format;
            return _result ?? ProxyConfigLintSubmittedConfigurationResult.Empty();
        }
    }

    private sealed class FixedConfigLintRuntimeStateSource : IProxyConfigLintRuntimeStateSource
    {
        private readonly IReadOnlyList<ProxyConfigLintRuntimeListenerState> _listeners;

        public FixedConfigLintRuntimeStateSource(IReadOnlyList<ProxyConfigLintRuntimeListenerState> listeners)
        {
            _listeners = listeners;
        }

        public IReadOnlyList<ProxyConfigLintRuntimeListenerState> GetListenerStates()
        {
            return _listeners;
        }
    }

    private sealed class FixedConfigLintMetricsSink : IProxyConfigLintMetricsSink
    {
        public IReadOnlyList<ConfigLintFinding> LastFindings { get; private set; } = [];

        public void ConfigLintRun(IReadOnlyList<ConfigLintFinding> findings)
        {
            LastFindings = findings;
        }
    }

    private static FixedRouteDiagnosticsListener RouteDiagnosticsListener(
        string name,
        string transport,
        int port,
        RuntimeListenerProtocols protocols,
        bool http3EnabledForTraffic)
    {
        return new FixedRouteDiagnosticsListener(
            name,
            transport,
            "127.0.0.1",
            port,
            true,
            protocols,
            http3EnabledForTraffic);
    }

    private static FixedRouteDiagnosticsRoute RouteDiagnosticsRoute(
        string name,
        string host,
        string pathPrefix,
        bool cacheEnabled = false,
        bool retryEnabled = false,
        bool circuitBreakerEnabled = false)
    {
        return new FixedRouteDiagnosticsRoute(
            "diag",
            name,
            host,
            pathPrefix,
            "Proxy",
            false,
            new ProxyRouteDiagnosticsMaintenancePolicy(false, null, "text/plain", ""),
            new ProxyRouteDiagnosticsHttpsRedirectPolicy(false, 308, null),
            new ProxyRouteDiagnosticsCanonicalHostPolicy(false, "", 308),
            new ProxyRouteDiagnosticsRedirectPolicy(302, "", "", false),
            new ProxyRouteDiagnosticsStaticResponse(200, "text/plain", ""),
            new ProxyRouteDiagnosticsPathRewrite("", "", ""),
            10 * 1024 * 1024,
            [new FixedRouteDiagnosticsUpstream("local", "http", RuntimeUpstreamProtocol.Http1, "127.0.0.1:5000", 1, circuitBreakerEnabled)],
            cacheEnabled,
            ["GET", "HEAD"],
            retryEnabled,
            ["GET", "HEAD"]);
    }

    private sealed record FixedRouteDiagnosticsConfigurationSnapshot(
        IReadOnlyList<IProxyRouteDiagnosticsListener> Listeners,
        IReadOnlyList<IProxyRouteDiagnosticsRoute> Routes)
        : IProxyRouteDiagnosticsConfigurationSnapshot;

    private sealed class FixedRouteDiagnosticsConfigurationSource
        : IProxyRouteDiagnosticsConfigurationSource
    {
        private readonly IProxyRouteDiagnosticsConfigurationSnapshot? _snapshot;

        public FixedRouteDiagnosticsConfigurationSource(IProxyRouteDiagnosticsConfigurationSnapshot? snapshot)
        {
            _snapshot = snapshot;
        }

        public ProxyRouteDiagnosticsConfigurationReadResult Read()
        {
            return _snapshot is null
                ? ProxyRouteDiagnosticsConfigurationReadResult.MissingConfiguration
                : ProxyRouteDiagnosticsConfigurationReadResult.Available(_snapshot);
        }
    }

    private sealed class FixedRouteDiagnosticsMatcher : IProxyRouteDiagnosticsMatcher
    {
        public IProxyRouteDiagnosticsRoute? Match(
            IReadOnlyList<IProxyRouteDiagnosticsRoute> routes,
            ProxyRouteDiagnosticsRequestHead requestHead)
        {
            return routes.FirstOrDefault(route =>
                HostMatches(route.Host, requestHead.Host)
                && requestHead.Path.StartsWith(route.PathPrefix, StringComparison.Ordinal));
        }

        private static bool HostMatches(string configuredHost, string requestHost)
        {
            return string.Equals(configuredHost, "*", StringComparison.Ordinal)
                || string.Equals(configuredHost, requestHost, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class FixedRouteDiagnosticsActionPolicy : IProxyRouteDiagnosticsActionPolicy
    {
        private readonly ProxyRouteDiagnosticsActionDecision _decision;

        public FixedRouteDiagnosticsActionPolicy(ProxyRouteDiagnosticsActionDecision decision)
        {
            _decision = decision;
        }

        public IProxyRouteDiagnosticsListener? LastListener { get; private set; }

        public ProxyRouteDiagnosticsRequestHead? LastRequestHead { get; private set; }

        public ProxyRouteDiagnosticsActionDecision Evaluate(
            IProxyRouteDiagnosticsRoute route,
            ProxyRouteDiagnosticsRequestHead requestHead,
            IProxyRouteDiagnosticsListener listener,
            bool isUpgradeRequest)
        {
            _ = route;
            _ = isUpgradeRequest;
            LastRequestHead = requestHead;
            LastListener = listener;
            return _decision;
        }
    }

    private sealed class FixedRouteDiagnosticsPathRewritePolicy : IProxyRouteDiagnosticsPathRewritePolicy
    {
        public string Apply(IProxyRouteDiagnosticsRoute route, string target, string path)
        {
            _ = route;
            _ = path;
            return target;
        }
    }

    private sealed class FixedRouteDiagnosticsMetricsSink : IProxyRouteDiagnosticsMetricsSink
    {
        public string? LastFailureReason { get; private set; }

        public void RouteMatchDryRun(string? failureReason)
        {
            LastFailureReason = failureReason;
        }
    }

    private sealed record FixedRouteDiagnosticsListener(
        string Name,
        string Transport,
        string Address,
        int Port,
        bool Enabled,
        RuntimeListenerProtocols Protocols,
        bool Http3EnabledForTraffic)
        : IProxyRouteDiagnosticsListener;

    private sealed record FixedRouteDiagnosticsRoute(
        string SiteName,
        string Name,
        string Host,
        string PathPrefix,
        string Action,
        bool MaintenanceEnabled,
        ProxyRouteDiagnosticsMaintenancePolicy Maintenance,
        ProxyRouteDiagnosticsHttpsRedirectPolicy HttpsRedirect,
        ProxyRouteDiagnosticsCanonicalHostPolicy CanonicalHost,
        ProxyRouteDiagnosticsRedirectPolicy Redirect,
        ProxyRouteDiagnosticsStaticResponse StaticResponse,
        ProxyRouteDiagnosticsPathRewrite PathRewrite,
        long MaxRequestBodyBytes,
        IReadOnlyList<IProxyRouteDiagnosticsUpstream> Upstreams,
        bool CacheEnabled,
        IReadOnlyList<string> CacheMethods,
        bool RetryEnabled,
        IReadOnlyList<string> RetryMethods)
        : IProxyRouteDiagnosticsRoute;

    private sealed record FixedRouteDiagnosticsUpstream(
        string Name,
        string Scheme,
        string Protocol,
        string Endpoint,
        int Weight,
        bool CircuitBreakerEnabled)
        : IProxyRouteDiagnosticsUpstream;

    private static RouteMatchDryRunResult.MatchedRouteResult Matched(RouteMatchDryRunResult result)
    {
        AssertEx.True(result is RouteMatchDryRunResult.MatchedRouteResult);
        return (RouteMatchDryRunResult.MatchedRouteResult)result;
    }

    private static RouteMatchDryRunResult.NoMatchingRouteResult NoMatchingRoute(RouteMatchDryRunResult result)
    {
        AssertEx.True(result is RouteMatchDryRunResult.NoMatchingRouteResult);
        return (RouteMatchDryRunResult.NoMatchingRouteResult)result;
    }

    private static RouteMatchDryRunResult.NoMatchingListenerResult NoMatchingListener(RouteMatchDryRunResult result)
    {
        AssertEx.True(result is RouteMatchDryRunResult.NoMatchingListenerResult);
        return (RouteMatchDryRunResult.NoMatchingListenerResult)result;
    }

    private static void AssertFinding(ConfigLintResult result, string code, string severity)
    {
        AssertEx.True(
            result.Findings.Any(finding => finding.Code == code && finding.Severity == severity),
            string.Join("; ", result.Findings.Select(static finding => $"{finding.Severity}:{finding.Code}:{finding.Message}")));
    }

    private static void AssertFinding(ConfigLintResponse result, string code, string severity)
    {
        AssertEx.True(
            result.Findings.Any(finding => finding.Code == code && finding.Severity == severity),
            string.Join("; ", result.Findings.Select(static finding => $"{finding.Severity}:{finding.Code}:{finding.Message}")));
    }

    private static void AssertNoFinding(ConfigLintResult result, string code)
    {
        AssertEx.False(
            result.Findings.Any(finding => finding.Code == code),
            string.Join("; ", result.Findings.Select(static finding => $"{finding.Severity}:{finding.Code}:{finding.Message}")));
    }

    private static void AssertAccepted(ConfigLintResult result, string? message = null)
    {
        AssertEx.True(
            result is ConfigLintResult.AcceptedResult,
            message ?? string.Join("; ", result.Findings.Select(static finding => finding.Message)));
    }

    private static void AssertRejected(ConfigLintResult result, string? message = null)
    {
        AssertEx.True(
            result is ConfigLintResult.RejectedResult,
            message ?? string.Join("; ", result.Findings.Select(static finding => finding.Message)));
    }
}
