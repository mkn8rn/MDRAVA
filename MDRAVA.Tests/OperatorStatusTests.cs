using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using MDRAVA.API.Controllers;
using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.INF.Configuration.Paths;
using MDRAVA.INF.Proxy.Connections;
using MDRAVA.INF.Proxy.Health;
using MDRAVA.API.Proxy.Hosting;
using MDRAVA.INF.Observability;
using MDRAVA.INF.Observability;
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

    public static void StatusInputReaderAssemblesNamedInputFromSources()
    {
        using var fixture = StatusFixture.Create();
        var listener = Listener();
        var upstream = Upstream();
        var route = ProxyRoute(upstream, healthEnabled: true);
        fixture.Store.Replace(Snapshot([listener], [route]));
        fixture.Runtime.ReplaceListeners([ListenerStatus(listener, ProxyListenerState.Active)], null);
        fixture.Health.RecordHealthCheckResult(
            HealthTarget(route, upstream),
            new HealthCheckSample(true, "status_200"),
            DateTimeOffset.UtcNow);
        var observedAtUtc = new DateTimeOffset(2026, 6, 10, 9, 0, 0, TimeSpan.Zero);

        var input = fixture.InputReader(timeProvider: new FixedTimeProvider(observedAtUtc)).Read();

        AssertEx.NotNull(input.Configuration);
        AssertEx.Equal(1, input.Configuration!.Version);
        AssertEx.Equal(1, input.Runtime.Listeners.Count);
        AssertEx.Equal(1, input.Upstreams.Count);
        AssertEx.Equal(UpstreamHealthState.Healthy, input.Upstreams[0].HealthState);
        AssertEx.Equal(ProxyStatusText.Healthy, input.LogPersistence.State);
        AssertEx.Equal(ProxyRuntimePreflightStatus.Unknown, input.RuntimePreflight);
        AssertEx.Equal(observedAtUtc, input.ObservedAtUtc);
        AssertEx.True(input.ConfigLint.Available);
    }

    public static void StatusRuntimeSummaryMapperProjectsOnlyResponseRuntimeFacts()
    {
        var listener = Listener();
        var startedAt = new DateTimeOffset(2026, 6, 10, 9, 10, 0, TimeSpan.Zero);
        var stoppedAt = startedAt.AddMinutes(2);
        var shutdownStartedAt = startedAt.AddMinutes(3);
        var shutdownDeadline = startedAt.AddMinutes(4);
        var reload = new ProxyListenerReloadResult(
            Succeeded: false,
            AttemptedAtUtc: startedAt,
            Added: 1,
            Removed: 0,
            Changed: 0,
            Unchanged: 0,
            Changes: [],
            Errors: ["bind_failed"]);
        var runtime = new ProxyRuntimeSnapshot(
            IsRunning: false,
            ListenerName: "main",
            Endpoint: "127.0.0.1:18080",
            StartedAt: startedAt,
            StoppedAt: stoppedAt,
            LastError: "listener failed",
            IsShuttingDown: true,
            ShutdownStartedAtUtc: shutdownStartedAt,
            ShutdownDeadlineUtc: shutdownDeadline)
        {
            Listeners = [ListenerStatus(listener, ProxyListenerState.Failed)],
            LastListenerReload = reload
        };

        var summary = ProxyStatusRuntimeSummaryMapper.FromRuntime(runtime);

        AssertEx.False(summary.ListenerLive);
        AssertEx.Equal("main", summary.ListenerName);
        AssertEx.Equal("127.0.0.1:18080", summary.Endpoint);
        AssertEx.Equal(startedAt, summary.StartedAt);
        AssertEx.Equal(stoppedAt, summary.StoppedAt);
        AssertEx.Equal("listener failed", summary.LastError);
        AssertEx.True(summary.IsShuttingDown);
        AssertEx.Equal(shutdownStartedAt, summary.ShutdownStartedAtUtc);
        AssertEx.Equal(shutdownDeadline, summary.ShutdownDeadlineUtc);
        AssertEx.Equal(1, summary.Listeners.Count);
        AssertEx.Equal(ProxyListenerState.Failed, summary.Listeners[0].State);
        AssertEx.Equal(reload, summary.LastListenerReload);
    }

    public static void StatusRuntimeSourceReadsRuntimeSummaryWithoutRuntimeSnapshot()
    {
        var listener = Listener();
        var runtime = new ProxyRuntimeState(TimeProvider.System);
        runtime.ReplaceListeners([ListenerStatus(listener, ProxyListenerState.Active)], null);
        IProxyStatusRuntimeStateSource source = runtime;

        var summary = source.ReadRuntimeSummary();

        AssertEx.True(summary.ListenerLive);
        AssertEx.Equal(listener.Name, summary.ListenerName);
        AssertEx.Equal($"{listener.Address}:{listener.Port}", summary.Endpoint);
        AssertEx.Equal(1, summary.Listeners.Count);
        AssertEx.Equal(ProxyListenerState.Active, summary.Listeners[0].State);
    }

    public static void StatusConfigurationSummaryMapperReadsRuntimeConfigurationFactsWithoutSnapshot()
    {
        var loadedAtUtc = new DateTimeOffset(2026, 6, 12, 8, 0, 0, TimeSpan.Zero);
        var listener = Listener();
        var route = StaticRoute();

        var summary = ProxyStatusConfigurationSummaryMapper.FromRuntimeConfiguration(
            version: 17,
            loadedAtUtc,
            listeners: [listener],
            routes: [route]);

        AssertEx.Equal(17, summary.Version);
        AssertEx.Equal(loadedAtUtc, summary.LoadedAtUtc);
        AssertEx.Equal(1, summary.ListenerCount);
        AssertEx.Equal(1, summary.RouteCount);
    }

    public static void StatusConfigurationSourceMapperShapesStatusFactsFromActiveConfiguration()
    {
        var listener = Listener();
        var route = StaticRoute();
        var snapshot = Snapshot([listener], [route]);

        var source = ProxyStatusConfigurationSourceMapper.FromConfiguration(snapshot);

        AssertEx.Equal(snapshot.Version, source.Version);
        AssertEx.Equal(snapshot.LoadedAtUtc, source.LoadedAtUtc);
        AssertEx.Equal(1, source.Listeners.Count);
        AssertEx.Equal(1, source.Routes.Count);
        AssertEx.True(source.ReadinessConfiguration.HasActiveConfiguration);
        AssertEx.Equal(snapshot.Version, source.ReadinessConfiguration.ConfigGeneration!.Value);
        AssertEx.Equal(snapshot.LoadedAtUtc, source.ReadinessConfiguration.ConfigurationLoadedAtUtc!.Value);
        AssertEx.Equal(1, source.ReadinessConfiguration.ConfiguredListeners.Count);
        AssertEx.Equal(1, source.ReadinessConfiguration.Routes.Count);
    }

    public static void StatusResponseBuilderBuildsResponseFromNamedInput()
    {
        var listener = Listener();
        var route = StaticRoute(cache: CachePolicy());
        var snapshot = Snapshot([listener], [route]);
        var runtime = new ProxyRuntimeSnapshot(
            true,
            "main",
            "127.0.0.1:18080",
            DateTimeOffset.UnixEpoch,
            null,
            null)
        {
            Listeners = [ListenerStatus(listener, ProxyListenerState.Active)]
        };
        var metrics = new ProxyMetrics().Snapshot();
        var observedAtUtc = new DateTimeOffset(2026, 6, 10, 9, 5, 0, TimeSpan.Zero);
        var runtimeSummary = ProxyStatusRuntimeSummaryMapper.FromRuntime(runtime);
        var http3 = Http3RuntimeSupport.ProjectRuntime(
            Http3SupportSourceMapper.FromConfiguration(snapshot.Listeners, snapshot.Routes),
            TestHttp3PlatformSupport.Supported,
            Http3SupportSourceMapper.FromRuntimeListeners(runtime.Listeners));
        var logPersistence = new ProxyLogPersistenceStatus(
            true,
            true,
            null,
            0,
            0,
            ProxyStatusText.Healthy,
            "ok",
            null,
            null);
        var cacheStatus = new ProxyCacheStatusResponse(3, 1024, 0, 0, 0, 0, 0, null, null, [], []);
        var preflight = ProxyRuntimePreflightStatus.Unknown;
        var readiness = ProxyStatusReadinessInputMapper.FromSources(
            ProxyStatusReadinessSourceMapper.FromSources(
                ProxyStatusReadinessConfigurationSourceMapper.FromConfiguration(snapshot),
                runtimeSummary,
                metrics,
                [],
                http3,
                logPersistence),
            cacheStatus,
            [],
            preflight,
            observedAtUtc);
        var input = new ProxyStatusInput(
            runtimeSummary,
            ProxyStatusConfigurationSummaryMapper.FromRuntimeConfiguration(
                snapshot.Version,
                snapshot.LoadedAtUtc,
                snapshot.Listeners,
                snapshot.Routes),
            metrics,
            [],
            http3,
            logPersistence,
            cacheStatus,
            [],
            preflight,
            observedAtUtc,
            readiness,
            ConfigLintStatus.Empty);

        var status = ProxyStatusResponseBuilder.Build(input);

        AssertEx.True(status.ListenerLive);
        AssertEx.Equal("main", status.ListenerName);
        AssertEx.Equal(1, status.ConfiguredListeners);
        AssertEx.Equal(1, status.ConfiguredRoutes);
        AssertEx.Equal(3, status.Subsystems.Cache.EntryCount);
        AssertEx.Equal(1024, status.Subsystems.Cache.ApproximateBytes);
        AssertEx.Equal("healthy", status.Readiness.State);
        AssertEx.Equal(observedAtUtc, status.Readiness.GeneratedAtUtc);
        AssertEx.Equal(ConfigLintStatus.Empty, status.ConfigLint);
    }

    public static void StatusReadinessSourceMapperConsumesRuntimeSummaryWithoutRuntimeSnapshot()
    {
        var listener = Listener();
        var reload = new ProxyListenerReloadResult(
            Succeeded: false,
            AttemptedAtUtc: DateTimeOffset.UnixEpoch,
            Added: 0,
            Removed: 0,
            Changed: 1,
            Unchanged: 0,
            Changes: [],
            Errors: ["bind_failed"]);
        var runtime = new ProxyStatusRuntimeSummary(
            ListenerLive: false,
            ListenerName: null,
            Endpoint: null,
            StartedAt: null,
            StoppedAt: DateTimeOffset.UnixEpoch,
            LastError: "bind_failed",
            IsShuttingDown: true,
            ShutdownStartedAtUtc: DateTimeOffset.UnixEpoch.AddMinutes(1),
            ShutdownDeadlineUtc: DateTimeOffset.UnixEpoch.AddMinutes(2),
            Listeners: [ListenerStatus(listener, ProxyListenerState.Failed)],
            LastListenerReload: reload);
        var metrics = new ProxyMetrics().Snapshot();
        var http3 = new RuntimeHttp3SupportProjection(
            "unknown",
            QuicListenerSupported: false,
            QuicConnectionSupported: false,
            "disabled",
            "disabled",
            EnabledForTraffic: false,
            QuicListenerReady: false,
            AltSvcConfigured: false,
            AltSvcActive: false,
            AltSvcMaxAgeSeconds: null,
            "not_configured",
            UdpQuicListenerIdentityModeled: true,
            "client_http3_default_enabled_for_eligible_tls_proxy_listeners");
        var logPersistence = ProxyLogPersistenceStatus.Unknown;

        var sources = ProxyStatusReadinessSourceMapper.FromSources(
            ProxyStatusReadinessConfigurationSourceMapper.FromConfiguration(null),
            runtime,
            metrics,
            upstreams: [],
            http3,
            logPersistence);

        AssertEx.False(sources.HasActiveConfiguration);
        AssertEx.False(sources.LastListenerReloadSucceeded!.Value);
        AssertEx.True(sources.LastListenerReloadFailed);
        AssertEx.Equal(1, sources.RuntimeListeners.Count);
        AssertEx.Equal(ProxyListenerState.Failed, sources.RuntimeListeners[0].State);
        AssertEx.False(sources.Shutdown.IsRunning);
        AssertEx.True(sources.Shutdown.IsShuttingDown);
        AssertEx.Equal(DateTimeOffset.UnixEpoch.AddMinutes(1), sources.Shutdown.ShutdownStartedAtUtc);
    }

    public static void StatusReadinessSourceMapperConsumesConfigurationSourceSetWithoutConfigurationSnapshot()
    {
        var configuration = new ProxyStatusReadinessConfigurationSourceSet(
            true,
            42,
            DateTimeOffset.UnixEpoch.AddHours(1),
            [new ProxyConfiguredListenerSummarySource(true, true, false, false)],
            [new ProxyRouteSummarySource("site-a", true, true, false)],
            new ProxyCertificateSummarySource(["cert-a"], []),
            new ProxyAcmeSummaryConfigurationSource(true, 1),
            new ProxyLimitConfigurationSummarySource(100, 4, 600));
        var runtime = new ProxyStatusRuntimeSummary(
            ListenerLive: true,
            ListenerName: "main",
            Endpoint: "127.0.0.1:18080",
            StartedAt: DateTimeOffset.UnixEpoch,
            StoppedAt: null,
            LastError: null,
            IsShuttingDown: false,
            ShutdownStartedAtUtc: null,
            ShutdownDeadlineUtc: null,
            Listeners: [],
            LastListenerReload: null);
        var http3 = new RuntimeHttp3SupportProjection(
            "unknown",
            QuicListenerSupported: false,
            QuicConnectionSupported: false,
            "disabled",
            "disabled",
            EnabledForTraffic: false,
            QuicListenerReady: false,
            AltSvcConfigured: false,
            AltSvcActive: false,
            AltSvcMaxAgeSeconds: null,
            "not_configured",
            UdpQuicListenerIdentityModeled: true,
            "client_http3_default_enabled_for_eligible_tls_proxy_listeners");

        var sources = ProxyStatusReadinessSourceMapper.FromSources(
            configuration,
            runtime,
            new ProxyMetrics().Snapshot(),
            upstreams: [],
            http3,
            ProxyLogPersistenceStatus.Unknown);

        AssertEx.True(sources.HasActiveConfiguration);
        AssertEx.Equal(42, sources.ConfigGeneration!.Value);
        AssertEx.Equal(DateTimeOffset.UnixEpoch.AddHours(1), sources.ConfigurationLoadedAtUtc!.Value);
        AssertEx.Equal(1, sources.ConfiguredListeners.Count);
        AssertEx.Equal(1, sources.Routes.Count);
        AssertEx.Equal("site-a", sources.Routes[0].SiteName);
        AssertEx.Equal("cert-a", AssertEx.NotNull(sources.Certificates).ReferencedCertificateIds[0]);
        AssertEx.True(AssertEx.NotNull(sources.Acme).Enabled);
        AssertEx.Equal(600, AssertEx.NotNull(sources.LimitConfiguration).RequestsPerMinutePerIp);
    }

    public static void StatusListenerAndRouteSourceMappersReadCollectionsWithoutConfigurationSnapshot()
    {
        var listener = Listener() with
        {
            Transport = RuntimeListenerTransport.Https,
            DefaultCertificateId = "status-cert",
            Protocols = RuntimeListenerProtocols.Http1AndHttp3
        };
        var upstream = Upstream() with
        {
            Scheme = "https",
            Protocol = RuntimeUpstreamProtocol.Http3
        };
        var route = ProxyRoute(upstream) with
        {
            Cache = CachePolicy()
        };

        var listeners = ProxyConfiguredListenerSummarySourceMapper.FromListeners([listener]);
        var routes = ProxyRouteSummarySourceMapper.FromRoutes([route]);

        AssertEx.Equal(1, listeners.Count);
        AssertEx.True(listeners[0].Enabled);
        AssertEx.True(listeners[0].Http1Enabled);
        AssertEx.False(listeners[0].Http2Enabled);
        AssertEx.True(listeners[0].Http3EnabledForTraffic);
        AssertEx.Equal(1, routes.Count);
        AssertEx.Equal("main", routes[0].SiteName);
        AssertEx.True(routes[0].IsProxyRoute);
        AssertEx.True(routes[0].CacheEnabled);
        AssertEx.True(routes[0].HasHttp3Upstream);
    }

    public static void StatusAcmeSummaryConfigurationMapperReadsAcmeOptionsWithoutConfigurationSnapshot()
    {
        var acme = new RuntimeAcmeOptions(
            Enabled: true,
            UseStaging: false,
            DirectoryUrl: "https://acme.example/directory",
            ContactEmails: ["ops@example.test"],
            TermsAccepted: true,
            StoragePath: "acme",
            RenewBeforeDays: 30,
            CheckIntervalMinutes: 720,
            RetryAfterMinutes: 60,
            Certificates:
            [
                new RuntimeAcmeCertificateOptions("first", true, ["first.example.test"], 30),
                new RuntimeAcmeCertificateOptions("second", false, ["second.example.test"], 30)
            ]);

        var source = ProxyAcmeSummaryConfigurationSourceMapper.FromConfiguration(acme);

        AssertEx.True(source.Enabled);
        AssertEx.Equal(1, source.ConfiguredCertificates);
    }

    public static void StatusCertificateSummarySourceMapperReadsListenersAndCertificatesWithoutSnapshot()
    {
        using var certificate = X509CertificateLoader.LoadPkcs12(
            TestCertificates.CreateSelfSignedPfxBytes("status-cert.example.test"),
            null);
        var runtimeCertificate = new RuntimeCertificate(
            "default",
            "default.pfx",
            "pfx",
            false,
            certificate,
            "manualPfx",
            ["status-cert.example.test"]);
        var listener = Listener() with
        {
            DefaultCertificateId = "default",
            SniCertificates = [new RuntimeSniCertificateBinding("alt.example.test", "alt")]
        };

        var source = ProxyCertificateSummarySourceMapper.FromConfiguration(
            [listener],
            new Dictionary<string, RuntimeCertificate>(StringComparer.OrdinalIgnoreCase)
            {
                ["default"] = runtimeCertificate
            });

        AssertEx.Equal(2, source.ReferencedCertificateIds.Count);
        AssertEx.Equal("default", source.ReferencedCertificateIds[0]);
        AssertEx.Equal("alt", source.ReferencedCertificateIds[1]);
        AssertEx.Equal(1, source.LoadedCertificates.Count);
        AssertEx.Equal("default", source.LoadedCertificates[0].Id);
        AssertEx.Equal(certificate.NotBefore, source.LoadedCertificates[0].NotBefore);
        AssertEx.Equal(certificate.NotAfter, source.LoadedCertificates[0].NotAfter);
    }

    public static void StatusLimitSummaryConfigurationMapperReadsLimitsWithoutConfigurationSnapshot()
    {
        var limits = new RuntimeLimits(
            MaxActiveClientConnections: 123,
            MaxConcurrentTlsHandshakes: 7,
            RequestsPerMinutePerIp: 45,
            UpgradeRequestsPerMinutePerIp: 9,
            MaxRequestHeadBytes: 32768,
            MaxHeaderCount: 128,
            MaxHeaderLineBytes: 8192,
            MaxRequestBodyBytes: 104857600,
            MaxPathBytes: 8192,
            ShutdownGracePeriod: TimeSpan.FromSeconds(15));

        var source = ProxyLimitSummarySourceMapper.FromConfiguration(limits);

        AssertEx.Equal(123, source.MaxActiveClientConnections);
        AssertEx.Equal(7, source.MaxConcurrentTlsHandshakes);
        AssertEx.Equal(45, source.RequestsPerMinutePerIp);
    }

    public static void ReadinessEvaluatorConsumesNarrowFactsWithoutRuntimeSnapshots()
    {
        var evaluatedAt = new DateTimeOffset(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);
        var input = new ProxyReadinessEvaluationInput(
            HasActiveConfiguration: true,
            ConfigGeneration: 77,
            IsShuttingDown: false,
            LastListenerReloadFailed: true,
            LogPersistenceState: ProxyStatusText.Healthy,
            RuntimePreflight: ProxyRuntimePreflightStatus.Unknown,
            Subsystems: HealthyReadinessSubsystems() with
            {
                Listeners = new ProxyListenerSubsystemSummary(1, 1, 1, 1, 0, 1, 0, 0, 0)
            },
            EvaluatedAtUtc: evaluatedAt);

        var readiness = ProxyReadinessEvaluator.Evaluate(input);

        AssertEx.Equal("degraded", readiness.State);
        AssertEx.True(readiness.Reasons.Contains("listener_start_failed"), string.Join(",", readiness.Reasons));
        AssertEx.True(readiness.Reasons.Contains("last_listener_reload_failed"), string.Join(",", readiness.Reasons));
        AssertEx.Equal(evaluatedAt, readiness.GeneratedAtUtc);
        AssertEx.Equal(77, readiness.ConfigGeneration);
    }

    public static void StatusReadinessBuilderConsumesNamedFactsWithoutRuntimeSnapshots()
    {
        var observedAt = new DateTimeOffset(2026, 6, 10, 8, 0, 0, TimeSpan.Zero);
        var input = new ProxyStatusReadinessInput(
            HasActiveConfiguration: true,
            ConfigGeneration: 88,
            ConfigurationLoadedAtUtc: observedAt.AddMinutes(-5),
            LastListenerReloadSucceeded: true,
            LastListenerReloadFailed: false,
            ConfiguredListeners:
            [
                new ProxyConfiguredListenerSummarySource(
                    Enabled: true,
                    Http1Enabled: true,
                    Http2Enabled: false,
                    Http3EnabledForTraffic: false)
            ],
            RuntimeListeners:
            [
                new ProxyRuntimeListenerSummarySource(IsQuic: false, ProxyListenerState.Active)
            ],
            Routes:
            [
                new ProxyRouteSummarySource(
                    SiteName: "main",
                    IsProxyRoute: true,
                    CacheEnabled: true,
                    HasHttp3Upstream: false)
            ],
            Certificates: null,
            Acme: new ProxyAcmeSummaryConfigurationSource(Enabled: false, ConfiguredCertificates: 0),
            Upstreams:
            [
                new ProxyUpstreamSummarySource(
                    UpstreamHealthState.Healthy,
                    HealthCheckEnabled: true,
                    CircuitBreakerEnabled: false,
                    CircuitBreakerRuntimeState.Disabled)
            ],
            LimitConfiguration: new ProxyLimitConfigurationSummarySource(
                MaxActiveClientConnections: 4096,
                MaxConcurrentTlsHandshakes: 16,
                RequestsPerMinutePerIp: 30),
            LimitRuntime: new ProxyLimitRuntimeSummarySource(
                ActiveConnections: 1,
                ActiveTlsHandshakes: 0,
                ActiveHttp2Streams: 0,
                ActiveHttp3Streams: 0,
                ActiveUpstreamHttp3Streams: 0),
            ClientHttp3Enabled: false,
            ClientHttp3Ready: false,
            Log: new ProxyLogSummarySource(
                AccessLogPersistenceEnabled: true,
                AdminAuditPersistenceEnabled: true,
                State: ProxyStatusText.Healthy,
                Reason: "ok"),
            Shutdown: new ProxyShutdownSummarySource(
                IsRunning: true,
                IsShuttingDown: false,
                ShutdownStartedAtUtc: null,
                ShutdownDeadlineUtc: null),
            CacheStatus: new ProxyCacheStatusResponse(1, 512, 0, 0, 0, 0, 0, null, null, [], []),
            AcmeStatuses: [],
            RuntimePreflight: ProxyRuntimePreflightStatus.Unknown,
            ObservedAtUtc: observedAt);

        var (readiness, subsystems) = ProxyStatusReadinessBuilder.Build(input);

        AssertEx.Equal("healthy", readiness.State);
        AssertEx.Equal(88, readiness.ConfigGeneration);
        AssertEx.Equal(observedAt, readiness.GeneratedAtUtc);
        AssertEx.Equal(1, subsystems.Listeners.Active);
        AssertEx.Equal(1, subsystems.Routes.CacheEnabledRoutes);
        AssertEx.Equal(1, subsystems.Cache.EntryCount);
        AssertEx.Equal(1, subsystems.Limits.ActiveConnections);
    }

    public static void SubsystemSummaryBuilderCountsNarrowListenerRouteAndUpstreamSources()
    {
        ProxyConfiguredListenerSummarySource[] configuredListeners =
        [
            new(Enabled: true, Http1Enabled: true, Http2Enabled: false, Http3EnabledForTraffic: true),
            new(Enabled: false, Http1Enabled: true, Http2Enabled: true, Http3EnabledForTraffic: false)
        ];
        ProxyRuntimeListenerSummarySource[] runtimeListeners =
        [
            new(IsQuic: false, ProxyListenerState.Active),
            new(IsQuic: true, ProxyListenerState.Active),
            new(IsQuic: false, ProxyListenerState.Failed),
            new(IsQuic: false, ProxyListenerState.Draining)
        ];
        ProxyRouteSummarySource[] routes =
        [
            new("main", IsProxyRoute: true, CacheEnabled: true, HasHttp3Upstream: false),
            new("main", IsProxyRoute: false, CacheEnabled: false, HasHttp3Upstream: true),
            new("api", IsProxyRoute: true, CacheEnabled: false, HasHttp3Upstream: false)
        ];
        ProxyUpstreamSummarySource[] upstreams =
        [
            new(UpstreamHealthState.Healthy, HealthCheckEnabled: true, CircuitBreakerEnabled: true, CircuitBreakerRuntimeState.Closed),
            new(UpstreamHealthState.Unhealthy, HealthCheckEnabled: true, CircuitBreakerEnabled: true, CircuitBreakerRuntimeState.Open),
            new(UpstreamHealthState.Unknown, HealthCheckEnabled: false, CircuitBreakerEnabled: false, CircuitBreakerRuntimeState.Disabled)
        ];

        var listeners = ProxySubsystemSummaryBuilder.BuildListeners(configuredListeners, runtimeListeners);
        var routeSummary = ProxySubsystemSummaryBuilder.BuildRoutes(routes);
        var upstreamSummary = ProxySubsystemSummaryBuilder.BuildUpstreams(upstreams);
        var circuitSummary = ProxySubsystemSummaryBuilder.BuildCircuits(upstreams);
        var protocolSummary = ProxySubsystemSummaryBuilder.BuildProtocols(
            configuredListeners,
            clientHttp3Enabled: true,
            clientHttp3Ready: false,
            routes);

        AssertEx.Equal(2, listeners.Configured);
        AssertEx.Equal(1, listeners.Enabled);
        AssertEx.Equal(2, listeners.Active);
        AssertEx.Equal(1, listeners.Failed);
        AssertEx.Equal(1, listeners.Draining);
        AssertEx.Equal(1, listeners.Http1Enabled);
        AssertEx.Equal(0, listeners.Http2Enabled);
        AssertEx.Equal(1, listeners.Http3Enabled);
        AssertEx.Equal(1, listeners.QuicReady);
        AssertEx.Equal(2, routeSummary.Sites);
        AssertEx.Equal(3, routeSummary.Routes);
        AssertEx.Equal(2, routeSummary.ProxyRoutes);
        AssertEx.Equal(1, routeSummary.GeneratedRoutes);
        AssertEx.Equal(1, routeSummary.CacheEnabledRoutes);
        AssertEx.Equal(3, upstreamSummary.Total);
        AssertEx.Equal(1, upstreamSummary.Healthy);
        AssertEx.Equal(1, upstreamSummary.Unhealthy);
        AssertEx.Equal(1, upstreamSummary.UnknownHealth);
        AssertEx.Equal(2, upstreamSummary.HealthChecksEnabled);
        AssertEx.Equal(2, circuitSummary.Enabled);
        AssertEx.Equal(1, circuitSummary.Open);
        AssertEx.Equal(0, circuitSummary.HalfOpen);
        AssertEx.Equal(1, circuitSummary.Closed);
        AssertEx.True(protocolSummary.ClientHttp1Enabled);
        AssertEx.False(protocolSummary.ClientHttp2Enabled);
        AssertEx.True(protocolSummary.ClientHttp3Enabled);
        AssertEx.False(protocolSummary.ClientHttp3Ready);
        AssertEx.True(protocolSummary.UpstreamHttp3Configured);
    }

    public static void SubsystemSummaryBuilderClassifiesCertificateIssuesFromNarrowSources()
    {
        var now = new DateTimeOffset(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);
        var source = new ProxyCertificateSummarySource(
            ReferencedCertificateIds:
            [
                "missing-cert",
                "expired-cert",
                "future-cert",
                "soon-cert"
            ],
            LoadedCertificates:
            [
                new ProxyCertificateValiditySource(
                    "expired-cert",
                    new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc)),
                new ProxyCertificateValiditySource(
                    "future-cert",
                    new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc)),
                new ProxyCertificateValiditySource(
                    "soon-cert",
                    new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc))
            ]);

        var summary = ProxySubsystemSummaryBuilder.BuildCertificates(source, now);

        AssertEx.Equal(4, summary.Configured);
        AssertEx.Equal(3, summary.Loaded);
        AssertEx.Equal(1, summary.MissingReferences);
        AssertEx.Equal(1, summary.Expired);
        AssertEx.Equal(1, summary.NotYetValid);
        AssertEx.Equal(1, summary.ExpiringSoon);
        AssertEx.NotNull(summary.LastIssue);
        AssertEx.Equal("certificate", summary.LastIssue!.Category);
        AssertEx.Equal("missing_reference", summary.LastIssue.Reason);
        AssertEx.Equal("missing-cert", summary.LastIssue.AffectedIdentity);
        AssertEx.Equal(now, summary.LastIssue.TimestampUtc);
    }

    public static void StatusReadinessReportsLogPersistenceFailureAsDegraded()
    {
        const string querySecret = "phase-49-query-secret";
        using var fixture = StatusFixture.Create();
        var listener = Listener();
        File.WriteAllText(Path.Combine(fixture.DataDirectory, "logs"), "not a directory");
        fixture.Store.Replace(Snapshot([listener], [StaticRoute()]));
        fixture.Runtime.ReplaceListeners([ListenerStatus(listener, ProxyListenerState.Active)], null);

        fixture.Writer.WriteAdminAudit(new ProxyAdminAuditEvent(
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
        var stoppedAtUtc = new DateTimeOffset(2026, 6, 10, 10, 35, 0, TimeSpan.Zero);
        using var fixture = StatusFixture.Create(new FixedTimeProvider(stoppedAtUtc));
        var listener = Listener();
        fixture.Store.Replace(Snapshot([listener], [StaticRoute()]));
        fixture.Runtime.ReplaceListeners([ListenerStatus(listener, ProxyListenerState.Failed)], null);

        var status = fixture.Controller().Get();

        AssertEx.Equal("not_ready", status.Readiness.State);
        AssertEx.True(status.Readiness.Reasons.Contains("no_active_listeners"), string.Join(",", status.Readiness.Reasons));
        AssertEx.Equal(1, status.Subsystems.Listeners.Failed);
        AssertEx.Equal(0, status.Subsystems.Listeners.Active);
        AssertEx.Equal(stoppedAtUtc, status.StoppedAt);
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
            HealthTarget(route, upstream),
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

        AssertEx.True(fixture.Circuit.TryAcquire(CircuitBreakerStatusSourceMapper.FromUpstream(upstream), out var lease));
        fixture.Circuit.RecordFailure(AssertEx.NotNull(lease), "connect_failure");
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
            new ProxyDataDirectoryPathSafety(),
            new RuntimePreflightProbe(path =>
                path.EndsWith("logs", StringComparison.OrdinalIgnoreCase)
                    ? new ProxyRuntimeDirectoryProbeResult(true, false, true, false, secret)
                    : new ProxyRuntimeDirectoryProbeResult(true, false, true, true, null)),
            TimeProvider.System);
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
            listener.Http3.ToStatus(),
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

    private static UpstreamHealthCheckTarget HealthTarget(RuntimeRoute route, RuntimeUpstream upstream)
    {
        return new UpstreamHealthCheckTarget(
            route.Name,
            upstream.Name,
            upstream.Endpoint,
            upstream.Identity,
            UpstreamTransportEndpointMapper.FromUpstream(upstream),
            route.HealthCheck.Path,
            route.HealthCheck.Interval,
            route.HealthCheck.Timeout,
            route.HealthCheck.HealthyThreshold,
            route.HealthCheck.UnhealthyThreshold);
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

    private static ProxySubsystemSummaries HealthyReadinessSubsystems()
    {
        return new ProxySubsystemSummaries(
            new ProxyConfigSubsystemSummary(true, 77, DateTimeOffset.UnixEpoch, true, "listener_reload_succeeded"),
            new ProxyListenerSubsystemSummary(1, 1, 1, 0, 0, 1, 0, 0, 0),
            new ProxyRouteSubsystemSummary(1, 1, 1, 0, 0),
            new ProxyCertificateSubsystemSummary(0, 0, 0, 0, 0, 0, null),
            new ProxyAcmeSubsystemSummary(false, 0, 0, 0, 0, null),
            new ProxyUpstreamSubsystemSummary(1, 1, 0, 0, 0),
            new ProxyCacheSubsystemSummary(false, 0, 0, 0),
            new ProxyCircuitSubsystemSummary(0, 0, 0, 0),
            new ProxyLimitSubsystemSummary(4096, 0, 16, 0, 0, 0, 0, 30),
            new ProxyLogSubsystemSummary(true, true, ProxyStatusText.Healthy, "ok"),
            new ProxyShutdownSubsystemSummary(true, false, null, null),
            new ProxyProtocolSubsystemSummary(true, false, false, false, false, []));
    }

    private sealed class StatusFixture : IDisposable
    {
        private StatusFixture(string dataDirectory, TimeProvider timeProvider)
        {
            DataDirectory = dataDirectory;
            Metrics = new ProxyMetrics();
            Store = new ProxyConfigurationStore();
            Runtime = new ProxyRuntimeState(timeProvider);
            Pool = new UpstreamConnectionPool(new UpstreamConnectionFactory(), Metrics, timeProvider);
            Circuit = new CircuitBreakerStore(Metrics, TimeProvider.System);
            Health = new UpstreamHealthStore(Metrics, Pool, Circuit);
            Cache = new ResponseCacheStore(TimeProvider.System);
            Acme = new AcmeCertificateStatusStore();
            Writer = new ProxyPersistentLogWriter(
                new MdravaDataDirectoryProvider(new MdravaDataDirectoryOptions
                {
                    DataDirectory = dataDirectory
                }),
                new ProxyLogPersistenceSettingsReader(new ProxyConfigurationLogPersistenceSettingsSource(Store)),
                NullLogger<ProxyPersistentLogWriter>.Instance,
                timeProvider);
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

        public static StatusFixture Create(TimeProvider? timeProvider = null)
        {
            var path = Path.Combine(Path.GetTempPath(), $"mdrava-status-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new StatusFixture(path, timeProvider ?? TimeProvider.System);
        }

        public ProxyStatusController Controller(ProxyRuntimePreflightService? preflight = null)
        {
            var statusOperations = ProxyStatusOperationFactory.Create(
                Runtime,
                Metrics,
                Store,
                Health,
                logPersistenceStore: Writer,
                cacheStore: Cache,
                acmeStatusStore: Acme,
                preflightService: preflight);
            return new ProxyStatusController(new ProxyStatusAdministrationService(statusOperations));
        }

        public ProxyStatusInputReader InputReader(
            ProxyRuntimePreflightService? preflight = null,
            TimeProvider? timeProvider = null)
        {
            return new ProxyStatusInputReader(
                Runtime,
                Metrics,
                Store,
                new ProxyStatusUpstreamHealthReader(Store, Health),
                FixedConfigLintOperations.Instance,
                Writer,
                new ProxyCacheStatusReader(
                    new ProxyCacheStatusConfigurationSource(Store),
                    new ProxyCacheRuntimeStatusSource(Cache)),
                new ProxyAcmeCertificateLifecycleStatusSource(Acme),
                preflight is null ? FixedRuntimePreflightSource.Instance : preflight,
                TestHttp3PlatformSupport.SupportedSource,
                timeProvider ?? TimeProvider.System);
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

    private sealed class FixedConfigLintOperations : IProxyConfigLintOperations
    {
        public static FixedConfigLintOperations Instance { get; } = new();

        public ConfigLintStatus LastActiveStatus => ConfigLintStatus.Empty;

        public ConfigLintResult LintActive()
        {
            return EmptyResult();
        }

        public ConfigLintResult LintSubmitted(ConfigLintRequest? request)
        {
            return EmptyResult();
        }

        private static ConfigLintResult EmptyResult()
        {
            return new ConfigLintResult(
                true,
                DateTimeOffset.UnixEpoch,
                ConfigLintSummary.Empty,
                [],
                []);
        }
    }

    private sealed class FixedRuntimePreflightSource : IProxyStatusRuntimePreflightSource
    {
        public static FixedRuntimePreflightSource Instance { get; } = new();

        public ProxyRuntimePreflightStatus ReadRuntimePreflight()
        {
            return ProxyRuntimePreflightStatus.Unknown;
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }
}
