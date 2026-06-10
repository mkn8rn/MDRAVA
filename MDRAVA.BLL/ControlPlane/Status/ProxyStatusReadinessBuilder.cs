using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.Caching;
using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.ControlPlane.Listeners;
using MDRAVA.BLL.ControlPlane.Metrics;
using MDRAVA.BLL.ControlPlane.Upstreams;

namespace MDRAVA.BLL.ControlPlane.Status;

public static class ProxyStatusReadinessBuilder
{
    public static (ProxyReadinessStatus Readiness, ProxySubsystemSummaries Subsystems) Build(
        ProxyConfigurationSnapshot? snapshot,
        ProxyRuntimeSnapshot runtime,
        ProxyMetricsSnapshot metrics,
        IReadOnlyList<ProxyUpstreamStatusResponse> upstreams,
        RuntimeHttp3SupportProjection http3,
        ProxyLogPersistenceStatus logPersistence,
        ProxyCacheStatusResponse? cacheStatus,
        IReadOnlyList<AcmeCertificateLifecycleStatus> acmeStatuses,
        ProxyRuntimePreflightStatus? runtimePreflight = null)
    {
        var now = DateTimeOffset.UtcNow;
        var runtimePreflightStatus = runtimePreflight ?? ProxyRuntimePreflightStatus.Unknown;
        var subsystems = BuildSubsystems(snapshot, runtime, metrics, upstreams, http3, logPersistence, cacheStatus, acmeStatuses, now);
        var readiness = ProxyReadinessEvaluator.Evaluate(new ProxyReadinessEvaluationInput(
            snapshot is not null,
            snapshot?.Version,
            runtime.IsShuttingDown,
            runtime.LastListenerReload is { Succeeded: false },
            logPersistence.State,
            runtimePreflightStatus,
            subsystems,
            now));
        return (readiness, subsystems);
    }

    private static ProxySubsystemSummaries BuildSubsystems(
        ProxyConfigurationSnapshot? snapshot,
        ProxyRuntimeSnapshot runtime,
        ProxyMetricsSnapshot metrics,
        IReadOnlyList<ProxyUpstreamStatusResponse> upstreams,
        RuntimeHttp3SupportProjection http3,
        ProxyLogPersistenceStatus logPersistence,
        ProxyCacheStatusResponse? cacheStatus,
        IReadOnlyList<AcmeCertificateLifecycleStatus> acmeStatuses,
        DateTimeOffset now)
    {
        var configuredListeners = ConfiguredListenerSources(snapshot);
        var runtimeListeners = RuntimeListenerSources(runtime);
        var routes = RouteSources(snapshot);
        var upstreamSources = UpstreamSources(upstreams);
        var limitRuntime = new ProxyLimitRuntimeSummarySource(
            metrics.ActiveConnections,
            metrics.ActiveTlsHandshakes,
            metrics.ActiveHttp2Streams,
            metrics.ActiveHttp3Streams,
            metrics.ActiveUpstreamHttp3Streams);

        return new ProxySubsystemSummaries(
            ProxySubsystemSummaryBuilder.BuildConfig(
                snapshot is not null,
                snapshot?.Version,
                snapshot?.LoadedAtUtc,
                runtime.LastListenerReload?.Succeeded),
            ProxySubsystemSummaryBuilder.BuildListeners(configuredListeners, runtimeListeners),
            ProxySubsystemSummaryBuilder.BuildRoutes(routes),
            ProxySubsystemSummaryBuilder.BuildCertificates(CertificateSource(snapshot), now),
            ProxySubsystemSummaryBuilder.BuildAcme(AcmeSource(snapshot), acmeStatuses, now),
            ProxySubsystemSummaryBuilder.BuildUpstreams(upstreamSources),
            ProxySubsystemSummaryBuilder.BuildCache(routes.Count(static route => route.CacheEnabled), cacheStatus),
            ProxySubsystemSummaryBuilder.BuildCircuits(upstreamSources),
            ProxySubsystemSummaryBuilder.BuildLimits(LimitConfigurationSource(snapshot), limitRuntime),
            ProxySubsystemSummaryBuilder.BuildLogs(new ProxyLogSummarySource(
                logPersistence.AccessLogEnabled,
                logPersistence.AdminAuditEnabled,
                logPersistence.State,
                logPersistence.Reason)),
            ProxySubsystemSummaryBuilder.BuildShutdown(new ProxyShutdownSummarySource(
                runtime.IsRunning,
                runtime.IsShuttingDown,
                runtime.ShutdownStartedAtUtc,
                runtime.ShutdownDeadlineUtc)),
            ProxySubsystemSummaryBuilder.BuildProtocols(
                configuredListeners,
                http3.EnabledForTraffic,
                http3.QuicListenerReady,
                routes));
    }

    private static IReadOnlyList<ProxyConfiguredListenerSummarySource> ConfiguredListenerSources(
        ProxyConfigurationSnapshot? snapshot)
    {
        return snapshot?.Listeners
            .Select(static listener => new ProxyConfiguredListenerSummarySource(
                listener.Enabled,
                listener.Protocols.HasFlag(RuntimeListenerProtocols.Http1),
                listener.Protocols.HasFlag(RuntimeListenerProtocols.Http2),
                listener.Http3.EnabledForTraffic))
            .ToArray() ?? [];
    }

    private static IReadOnlyList<ProxyRuntimeListenerSummarySource> RuntimeListenerSources(
        ProxyRuntimeSnapshot runtime)
    {
        return runtime.Listeners
            .Select(static listener => new ProxyRuntimeListenerSummarySource(
                string.Equals(listener.Kind, "quic", StringComparison.OrdinalIgnoreCase),
                listener.State))
            .ToArray();
    }

    private static IReadOnlyList<ProxyRouteSummarySource> RouteSources(ProxyConfigurationSnapshot? snapshot)
    {
        return snapshot?.Routes
            .Select(static route => new ProxyRouteSummarySource(
                route.SiteName,
                route.Action == RuntimeRouteAction.Proxy,
                route.Cache.Enabled,
                route.Upstreams.Any(static upstream => RuntimeUpstreamProtocol.IsHttp3(upstream.Protocol))))
            .ToArray() ?? [];
    }

    private static ProxyCertificateSummarySource? CertificateSource(ProxyConfigurationSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return null;
        }

        List<string> referenced = [];
        foreach (var listener in snapshot.Listeners)
        {
            if (!string.IsNullOrWhiteSpace(listener.DefaultCertificateId))
            {
                referenced.Add(listener.DefaultCertificateId);
            }

            foreach (var binding in listener.SniCertificates)
            {
                referenced.Add(binding.CertificateId);
            }
        }

        return new ProxyCertificateSummarySource(
            referenced,
            snapshot.Certificates.Values
                .Select(static certificate => new ProxyCertificateValiditySource(
                    certificate.Id,
                    certificate.Certificate.NotBefore,
                    certificate.Certificate.NotAfter))
                .ToArray());
    }

    private static ProxyAcmeSummaryConfigurationSource? AcmeSource(ProxyConfigurationSnapshot? snapshot)
    {
        return snapshot is null
            ? null
            : new ProxyAcmeSummaryConfigurationSource(
                snapshot.Acme.Enabled,
                snapshot.Acme.Certificates.Count(static certificate => certificate.Enabled));
    }

    private static IReadOnlyList<ProxyUpstreamSummarySource> UpstreamSources(
        IReadOnlyList<ProxyUpstreamStatusResponse> upstreams)
    {
        return upstreams
            .Select(static upstream => new ProxyUpstreamSummarySource(
                upstream.HealthState,
                upstream.HealthCheckEnabled,
                upstream.CircuitBreaker.Enabled,
                upstream.CircuitBreaker.State))
            .ToArray();
    }

    private static ProxyLimitConfigurationSummarySource? LimitConfigurationSource(ProxyConfigurationSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return null;
        }

        return new ProxyLimitConfigurationSummarySource(
            snapshot.Limits.MaxActiveClientConnections,
            snapshot.Limits.MaxConcurrentTlsHandshakes,
            snapshot.Limits.RequestsPerMinutePerIp);
    }
}
