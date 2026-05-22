using System.Text.Json;
using MDRAVA.API.Controllers;
using MDRAVA.API.Proxy.Acme;
using MDRAVA.API.Proxy.Caching;
using MDRAVA.INF.Configuration.Paths;
using MDRAVA.API.Proxy.Configuration.Storage;
using MDRAVA.API.Proxy.Connections;
using MDRAVA.API.Proxy.Health;
using MDRAVA.API.Proxy.Hosting;
using MDRAVA.API.Proxy.Metrics;
using MDRAVA.API.Proxy.Observability;
using MDRAVA.API.Proxy.Resilience;
using MDRAVA.API.Proxy.Runtime;
using Microsoft.Extensions.Logging.Abstractions;

namespace MDRAVA.Tests;

internal static class OperatorStatusTests
{
    private const string AdminToken = "phase-49-admin-token";

    public static void StatusReadinessReportsHealthyForActiveSnapshot()
    {
        using var fixture = StatusFixture.Create();
        var listener = Listener();
        fixture.Store.Replace(Snapshot([listener], [StaticRoute(cache: CachePolicy())]));
        fixture.Runtime.ReplaceListeners([ListenerStatus(listener, ProxyListenerState.Active)], null);

        var status = fixture.Controller().Get();

        AssertEx.Equal("healthy", status.Readiness.State);
        AssertEx.Equal(0, status.Readiness.Reasons.Count);
        AssertEx.Equal(1, status.ConfigVersion);
        AssertEx.Equal(1, status.Subsystems.Listeners.Active);
        AssertEx.Equal(1, status.Subsystems.Routes.CacheEnabledRoutes);
        AssertEx.True(status.Subsystems.Cache.Enabled);
        AssertEx.Equal(4096, status.Subsystems.Limits.MaxActiveClientConnections);
        AssertEx.Equal("healthy", status.Subsystems.Logs.State);
    }

    public static void StatusReadinessReportsLogPersistenceFailureAsDegraded()
    {
        const string querySecret = "phase-49-query-secret";
        using var fixture = StatusFixture.Create();
        var listener = Listener();
        File.WriteAllText(Path.Combine(fixture.DataDirectory, "logs"), "not a directory");
        fixture.Store.Replace(Snapshot([listener], [StaticRoute()]));
        fixture.Runtime.ReplaceListeners([ListenerStatus(listener, ProxyListenerState.Active)], null);

        fixture.Writer.WriteAdminAudit(new AdminAuditEvent(
            DateTimeOffset.UtcNow,
            "GET",
            $"/admin/proxy/status?token={querySecret}",
            "127.0.0.1",
            "invalid",
            403,
            false));

        var status = fixture.Controller().Get();
        var text = JsonSerializer.Serialize(status.Readiness) + JsonSerializer.Serialize(status.Subsystems);

        AssertEx.Equal("degraded", status.Readiness.State);
        AssertEx.True(status.Readiness.Reasons.Contains("log_persistence_degraded"), string.Join(",", status.Readiness.Reasons));
        AssertEx.Equal("degraded", status.Subsystems.Logs.State);
        AssertEx.False(text.Contains(querySecret, StringComparison.Ordinal), text);
        AssertEx.False(text.Contains(AdminToken, StringComparison.Ordinal), text);
        AssertEx.False(text.Contains("Authorization", StringComparison.OrdinalIgnoreCase), text);
        AssertEx.False(text.Contains("Cookie", StringComparison.OrdinalIgnoreCase), text);
    }

    public static void StatusReadinessReportsNoActiveListenerAsNotReady()
    {
        using var fixture = StatusFixture.Create();
        var listener = Listener();
        fixture.Store.Replace(Snapshot([listener], [StaticRoute()]));
        fixture.Runtime.ReplaceListeners([ListenerStatus(listener, ProxyListenerState.Failed)], null);

        var status = fixture.Controller().Get();

        AssertEx.Equal("not_ready", status.Readiness.State);
        AssertEx.True(status.Readiness.Reasons.Contains("no_active_listeners"), string.Join(",", status.Readiness.Reasons));
        AssertEx.Equal(1, status.Subsystems.Listeners.Failed);
        AssertEx.Equal(0, status.Subsystems.Listeners.Active);
    }

    public static void StatusReadinessReportsFailedListenerReloadWithoutLosingActiveConfig()
    {
        using var fixture = StatusFixture.Create();
        var listener = Listener();
        var active = ListenerStatus(listener, ProxyListenerState.Active);
        fixture.Store.Replace(Snapshot([listener], [StaticRoute()]));
        fixture.Runtime.ReplaceListeners(
            [active],
            new ProxyListenerReloadResult(
                false,
                DateTimeOffset.UtcNow,
                Added: 0,
                Removed: 0,
                Changed: 0,
                Unchanged: 1,
                Changes: [],
                Errors: ["raw bind failure that should not be copied into readiness"]));

        var status = fixture.Controller().Get();

        AssertEx.Equal("degraded", status.Readiness.State);
        AssertEx.True(status.Readiness.Reasons.Contains("last_listener_reload_failed"), string.Join(",", status.Readiness.Reasons));
        AssertEx.Equal(1, status.Readiness.ConfigGeneration);
        AssertEx.True(status.Subsystems.Config.Active);
        AssertEx.False(status.Subsystems.Config.LastListenerReloadSucceeded!.Value);
        AssertEx.Equal("listener_reload_failed", status.Subsystems.Config.LastListenerReloadReason);
        AssertEx.Equal(1, status.Subsystems.Listeners.Active);
    }

    public static void StatusReadinessSummarizesUnhealthyUpstreams()
    {
        using var fixture = StatusFixture.Create();
        var listener = Listener();
        var upstream = Upstream();
        var route = ProxyRoute(upstream, healthEnabled: true);
        fixture.Store.Replace(Snapshot([listener], [route]));
        fixture.Runtime.ReplaceListeners([ListenerStatus(listener, ProxyListenerState.Active)], null);
        fixture.Health.RecordHealthCheckResult(
            route,
            upstream,
            new HealthCheckSample(false, "status_500"),
            DateTimeOffset.UtcNow);

        var status = fixture.Controller().Get();

        AssertEx.Equal("degraded", status.Readiness.State);
        AssertEx.True(status.Readiness.Reasons.Contains("upstream_unhealthy"), string.Join(",", status.Readiness.Reasons));
        AssertEx.Equal(1, status.Subsystems.Upstreams.Total);
        AssertEx.Equal(1, status.Subsystems.Upstreams.Unhealthy);
        AssertEx.Equal(1, status.Subsystems.Upstreams.HealthChecksEnabled);
    }

    public static void StatusReadinessSummarizesOpenCircuits()
    {
        using var fixture = StatusFixture.Create();
        var listener = Listener();
        var upstream = Upstream() with
        {
            CircuitBreaker = new RuntimeCircuitBreakerPolicy(
                true,
                1,
                TimeSpan.FromSeconds(60),
                TimeSpan.FromSeconds(30),
                1,
                [])
        };
        var route = ProxyRoute(upstream);
        fixture.Store.Replace(Snapshot([listener], [route]));
        fixture.Runtime.ReplaceListeners([ListenerStatus(listener, ProxyListenerState.Active)], null);

        AssertEx.True(fixture.Circuit.TryAcquire(upstream, out var lease));
        fixture.Circuit.RecordFailure(lease, "connect_failure");
        var status = fixture.Controller().Get();

        AssertEx.Equal("degraded", status.Readiness.State);
        AssertEx.True(status.Readiness.Reasons.Contains("circuit_not_closed"), string.Join(",", status.Readiness.Reasons));
        AssertEx.Equal(1, status.Subsystems.Circuits.Enabled);
        AssertEx.Equal(1, status.Subsystems.Circuits.Open);
    }

    public static void StatusSubsystemsKeepUnsupportedHttp3FeaturesExplicit()
    {
        using var fixture = StatusFixture.Create();
        var listener = Listener();
        fixture.Store.Replace(Snapshot([listener], [StaticRoute()]));
        fixture.Runtime.ReplaceListeners([ListenerStatus(listener, ProxyListenerState.Active)], null);

        var protocols = fixture.Controller().Get().Subsystems.Protocols;

        AssertEx.False(protocols.ClientHttp3Enabled);
        AssertEx.True(
            protocols.UnsupportedHttp3Features.SequenceEqual(RuntimeHttp3UnsupportedFeatureCodes.StatusSummary),
            string.Join(",", protocols.UnsupportedHttp3Features));
    }

    public static void StatusReadinessReportsCertificateIssueSummaryWithoutSecrets()
    {
        const string missingCertificateId = "public-cert";
        using var fixture = StatusFixture.Create();
        var listener = Listener() with
        {
            Transport = RuntimeListenerTransport.Https,
            DefaultCertificateId = missingCertificateId,
            Http3Enablement = RuntimeHttp3Enablement.Disabled
        };
        fixture.Store.Replace(Snapshot([listener], [StaticRoute()]));
        fixture.Runtime.ReplaceListeners([ListenerStatus(listener, ProxyListenerState.Active)], null);

        var status = fixture.Controller().Get();
        var text = JsonSerializer.Serialize(status.Readiness) + JsonSerializer.Serialize(status.Subsystems);

        AssertEx.Equal("degraded", status.Readiness.State);
        AssertEx.True(status.Readiness.Reasons.Contains("certificate_reference_missing"), string.Join(",", status.Readiness.Reasons));
        AssertEx.Equal(1, status.Subsystems.Certificates.MissingReferences);
        AssertEx.NotNull(status.Subsystems.Certificates.LastIssue);
        AssertEx.Equal("certificate", status.Subsystems.Certificates.LastIssue!.Category);
        AssertEx.Equal("missing_reference", status.Subsystems.Certificates.LastIssue.Reason);
        AssertEx.Equal(missingCertificateId, status.Subsystems.Certificates.LastIssue.AffectedIdentity);
        AssertEx.False(text.Contains(AdminToken, StringComparison.Ordinal), text);
        AssertEx.False(text.Contains("Authorization", StringComparison.OrdinalIgnoreCase), text);
        AssertEx.False(text.Contains("Cookie", StringComparison.OrdinalIgnoreCase), text);
        AssertEx.False(text.Contains("private-key", StringComparison.OrdinalIgnoreCase), text);
    }

    public static void StatusReadinessReportsAcmeLastIssueWithoutRawErrorSummary()
    {
        const string rawErrorSecret = "phase-50-acme-token-secret";
        using var fixture = StatusFixture.Create();
        var listener = Listener();
        var acme = new RuntimeAcmeOptions(
            true,
            true,
            "https://acme.invalid/directory",
            [],
            true,
            "acme",
            30,
            720,
            60,
            [new RuntimeAcmeCertificateOptions("acme-cert", true, ["example.test"], 30)]);
        fixture.Store.Replace(Snapshot([listener], [StaticRoute()], acme: acme));
        fixture.Runtime.ReplaceListeners([ListenerStatus(listener, ProxyListenerState.Active)], null);
        fixture.Acme.Upsert(new AcmeCertificateLifecycleStatus(
            "acme-cert",
            true,
            ["example.test"],
            false,
            "acme",
            null,
            null,
            null,
            DateTimeOffset.UtcNow.AddMinutes(-2),
            null,
            DateTimeOffset.UtcNow.AddMinutes(-2),
            DateTimeOffset.UtcNow.AddMinutes(30),
            "failed",
            $"raw ACME exception with {rawErrorSecret} and C:\\private\\account.json"));

        var status = fixture.Controller().Get();
        var text = JsonSerializer.Serialize(status.Readiness) + JsonSerializer.Serialize(status.Subsystems);

        AssertEx.Equal("degraded", status.Readiness.State);
        AssertEx.True(status.Readiness.Reasons.Contains("acme_degraded"), string.Join(",", status.Readiness.Reasons));
        AssertEx.True(status.Subsystems.Acme.Enabled);
        AssertEx.Equal(1, status.Subsystems.Acme.Configured);
        AssertEx.Equal(1, status.Subsystems.Acme.Failed);
        AssertEx.Equal(1, status.Subsystems.Acme.RenewalBackoff);
        AssertEx.NotNull(status.Subsystems.Acme.LastIssue);
        AssertEx.Equal("acme", status.Subsystems.Acme.LastIssue!.Category);
        AssertEx.Equal("failed", status.Subsystems.Acme.LastIssue.Reason);
        AssertEx.Equal("acme-cert", status.Subsystems.Acme.LastIssue.AffectedIdentity);
        AssertEx.False(text.Contains(rawErrorSecret, StringComparison.Ordinal), text);
        AssertEx.False(text.Contains("account.json", StringComparison.OrdinalIgnoreCase), text);
        AssertEx.False(text.Contains("example.test", StringComparison.OrdinalIgnoreCase), text);
        AssertEx.False(text.Contains("Authorization", StringComparison.OrdinalIgnoreCase), text);
        AssertEx.False(text.Contains("Cookie", StringComparison.OrdinalIgnoreCase), text);
    }

    public static void StatusReadinessReportsRuntimePreflightWarningsWithoutSecrets()
    {
        const string secret = "phase-52-preflight-secret";
        using var fixture = StatusFixture.Create();
        var listener = Listener();
        fixture.Store.Replace(Snapshot([listener], [StaticRoute()]));
        fixture.Runtime.ReplaceListeners([ListenerStatus(listener, ProxyListenerState.Active)], null);
        var preflight = new ProxyRuntimePreflightService(
            new MdravaDataDirectoryProvider(new MdravaDataDirectoryOptions
            {
                DataDirectory = fixture.DataDirectory
            }),
            new RuntimePreflightProbe(path =>
                path.EndsWith("logs", StringComparison.OrdinalIgnoreCase)
                    ? new ProxyRuntimeDirectoryProbeResult(true, false, true, false, secret)
                    : new ProxyRuntimeDirectoryProbeResult(true, false, true, true, null)));
        preflight.RunStartupChecks();

        var status = fixture.Controller(preflight).Get();
        var text = JsonSerializer.Serialize(status.Readiness) + JsonSerializer.Serialize(status.RuntimePreflight);

        AssertEx.Equal("degraded", status.Readiness.State);
        AssertEx.True(status.Readiness.Reasons.Contains("runtime_preflight_degraded"), string.Join(",", status.Readiness.Reasons));
        AssertEx.Equal("degraded", status.RuntimePreflight.State);
        AssertEx.True(status.RuntimePreflight.Reasons.Contains("directory_not_writable"), string.Join(",", status.RuntimePreflight.Reasons));
        AssertEx.False(text.Contains(secret, StringComparison.Ordinal), text);
        AssertEx.False(text.Contains(fixture.DataDirectory, StringComparison.OrdinalIgnoreCase), text);
        AssertEx.False(text.Contains("Authorization", StringComparison.OrdinalIgnoreCase), text);
        AssertEx.False(text.Contains("Cookie", StringComparison.OrdinalIgnoreCase), text);
    }

    private sealed class RuntimePreflightProbe : IProxyRuntimeDirectoryProbe
    {
        private readonly Func<string, ProxyRuntimeDirectoryProbeResult> _probe;

        public RuntimePreflightProbe(Func<string, ProxyRuntimeDirectoryProbeResult> probe)
        {
            _probe = probe;
        }

        public ProxyRuntimeDirectoryProbeResult Probe(string path, bool createIfMissing)
        {
            return _probe(path);
        }
    }

    private static ProxyConfigurationSnapshot Snapshot(
        IReadOnlyList<RuntimeListener> listeners,
        IReadOnlyList<RuntimeRoute> routes,
        IReadOnlyDictionary<string, RuntimeCertificate>? certificates = null,
        RuntimeAcmeOptions? acme = null)
    {
        return new ProxyConfigurationSnapshot(
            1,
            DateTimeOffset.UtcNow,
            "tests",
            [],
            new ProxyConfigurationDiscovery(
                new ProxyFilesystemLayout("tests", "tests/config", "tests/config/sites", "tests/logs", "tests/certs", "tests/state", "tests/config/proxy.json"),
                [],
                [],
                []),
            new RuntimeAdminSecurityOptions([], true, true, AdminToken, "MDRAVA_ADMIN_TOKEN", "configured", 100),
            acme ?? new RuntimeAcmeOptions(false, true, "", [], false, "acme", 30, 720, 60, []),
            Timeouts(),
            new RuntimeConnectionLimits(100, 16, 1024),
            new RuntimeObservabilityOptions(true, 100, new RuntimeLogPersistenceOptions(true, true, 1_048_576, 8)),
            new RuntimeLimits(4096, 128, 240, 30, 32768, 128, 8192, 104857600, 8192, TimeSpan.FromSeconds(15)),
            new RuntimeForwardedHeadersOptions(true, []),
            certificates ?? new Dictionary<string, RuntimeCertificate>(StringComparer.OrdinalIgnoreCase),
            listeners,
            routes);
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

    private static ProxyListenerStatus ListenerStatus(RuntimeListener listener, ProxyListenerState state)
    {
        var identity = listener.Identity;
        return new ProxyListenerStatus(
            listener.Name,
            identity.Key,
            identity.BindKey,
            "tcp",
            listener.Address,
            listener.Port,
            listener.Transport.ToString().ToLowerInvariant(),
            listener.Transport == RuntimeListenerTransport.Https,
            listener.Protocols.ToConfigText(),
            listener.Http3,
            listener.Http2Limits.MaxConcurrentStreams,
            listener.Http2Limits.MaxHeaderListBytes,
            listener.Http2Limits.MaxFrameSize,
            state,
            ActiveConnections: 0,
            state == ProxyListenerState.Active ? DateTimeOffset.UtcNow : null,
            state == ProxyListenerState.Active ? null : DateTimeOffset.UtcNow,
            state == ProxyListenerState.Failed ? "bind_failed" : null);
    }

    private static RuntimeRoute StaticRoute(RuntimeCachePolicy? cache = null)
    {
        return BaseRoute(RuntimeRouteAction.StaticResponse, [], cache ?? RuntimeCachePolicy.Disabled);
    }

    private static RuntimeRoute ProxyRoute(RuntimeUpstream upstream, bool healthEnabled = false)
    {
        return BaseRoute(RuntimeRouteAction.Proxy, [upstream], RuntimeCachePolicy.Disabled, healthEnabled);
    }

    private static RuntimeRoute BaseRoute(
        RuntimeRouteAction action,
        IReadOnlyList<RuntimeUpstream> upstreams,
        RuntimeCachePolicy cache,
        bool healthEnabled = false)
    {
        return new RuntimeRoute(
            "main",
            "*",
            "/",
            action,
            "round-robin",
            new RuntimeHealthCheckOptions(healthEnabled, "/health", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), 1, 1),
            upstreams,
            new RuntimeHttpsRedirectPolicy(false, 308, null),
            new RuntimeCanonicalHostPolicy(false, "", 308),
            RuntimeHeaderPolicy.Empty,
            new RuntimePathRewritePolicy("", "", ""),
            new RuntimeRedirectPolicy(308, "", "", true),
            new RuntimeStaticResponse(200, "text/plain; charset=utf-8", "ok"),
            new RuntimeMaintenancePolicy(false, null, "text/plain; charset=utf-8", "Service Unavailable"),
            cache,
            new RuntimeRouteResolvedOptions(104857600, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), true))
        {
            SiteName = "main"
        };
    }

    private static RuntimeUpstream Upstream()
    {
        return new RuntimeUpstream(
            "main",
            "upstream",
            "http",
            RuntimeUpstreamProtocol.Http1,
            "127.0.0.1",
            5000,
            1,
            RuntimeUpstreamTlsOptions.Default);
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

    private sealed class StatusFixture : IDisposable
    {
        private StatusFixture(string dataDirectory)
        {
            DataDirectory = dataDirectory;
            Metrics = new ProxyMetrics();
            Store = new ProxyConfigurationStore();
            Runtime = new ProxyRuntimeState();
            Pool = new UpstreamConnectionPool(new UpstreamConnectionFactory(), Metrics);
            Circuit = new CircuitBreakerStore(Metrics, TimeProvider.System);
            Health = new UpstreamHealthStore(Metrics, Pool, Circuit);
            Cache = new ResponseCacheStore(TimeProvider.System);
            Acme = new AcmeCertificateStatusStore();
            Writer = new ProxyPersistentLogWriter(
                new MdravaDataDirectoryProvider(new MdravaDataDirectoryOptions
                {
                    DataDirectory = dataDirectory
                }),
                Store,
                NullLogger<ProxyPersistentLogWriter>.Instance);
        }

        public string DataDirectory { get; }

        public ProxyMetrics Metrics { get; }

        public ProxyConfigurationStore Store { get; }

        public ProxyRuntimeState Runtime { get; }

        public UpstreamConnectionPool Pool { get; }

        public CircuitBreakerStore Circuit { get; }

        public UpstreamHealthStore Health { get; }

        public ResponseCacheStore Cache { get; }

        public AcmeCertificateStatusStore Acme { get; }

        public ProxyPersistentLogWriter Writer { get; }

        public static StatusFixture Create()
        {
            var path = Path.Combine(Path.GetTempPath(), $"mdrava-status-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new StatusFixture(path);
        }

        public ProxyStatusController Controller(ProxyRuntimePreflightService? preflight = null)
        {
            return new ProxyStatusController(
                Runtime,
                Metrics,
                Store,
                Health,
                logWriter: Writer,
                cacheStore: Cache,
                acmeStatusStore: Acme,
                preflightService: preflight);
        }

        public void Dispose()
        {
            Pool.Dispose();
            try
            {
                if (Directory.Exists(DataDirectory))
                {
                    Directory.Delete(DataDirectory, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
