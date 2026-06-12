using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.Caching;
using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.ControlPlane.Listeners;
using MDRAVA.BLL.ControlPlane.Metrics;

namespace MDRAVA.BLL.ControlPlane.Status;

public static class ProxyStatusReadinessInputMapper
{
    public static ProxyStatusReadinessInput FromSources(
        ProxyConfigurationSnapshot? configuration,
        ProxyRuntimeSnapshot runtime,
        ProxyMetricsSnapshot metrics,
        IReadOnlyList<ProxyUpstreamStatusResponse> upstreams,
        RuntimeHttp3SupportProjection http3,
        ProxyLogPersistenceStatus logPersistence,
        ProxyCacheStatusResponse? cacheStatus,
        IReadOnlyList<AcmeCertificateLifecycleStatus> acmeStatuses,
        ProxyRuntimePreflightStatus runtimePreflight,
        DateTimeOffset observedAtUtc)
    {
        return new ProxyStatusReadinessInput(
            configuration is not null,
            configuration?.Version,
            configuration?.LoadedAtUtc,
            runtime.LastListenerReload?.Succeeded,
            runtime.LastListenerReload is { Succeeded: false },
            ConfiguredListenerSources(configuration),
            RuntimeListenerSources(runtime),
            RouteSources(configuration),
            CertificateSource(configuration),
            AcmeSource(configuration),
            UpstreamSources(upstreams),
            LimitConfigurationSource(configuration),
            new ProxyLimitRuntimeSummarySource(
                metrics.ActiveConnections,
                metrics.ActiveTlsHandshakes,
                metrics.ActiveHttp2Streams,
                metrics.ActiveHttp3Streams,
                metrics.ActiveUpstreamHttp3Streams),
            http3.EnabledForTraffic,
            http3.QuicListenerReady,
            new ProxyLogSummarySource(
                logPersistence.AccessLogEnabled,
                logPersistence.AdminAuditEnabled,
                logPersistence.State,
                logPersistence.Reason),
            new ProxyShutdownSummarySource(
                runtime.IsRunning,
                runtime.IsShuttingDown,
                runtime.ShutdownStartedAtUtc,
                runtime.ShutdownDeadlineUtc),
            cacheStatus,
            acmeStatuses,
            runtimePreflight,
            observedAtUtc);
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
