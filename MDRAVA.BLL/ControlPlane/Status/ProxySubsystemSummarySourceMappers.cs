using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.HealthChecks;
using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.ControlPlane.Listeners;
using MDRAVA.BLL.ControlPlane.Metrics;

namespace MDRAVA.BLL.ControlPlane.Status;

public static class ProxyConfiguredListenerSummarySourceMapper
{
    public static IReadOnlyList<ProxyConfiguredListenerSummarySource> FromListeners(
        IReadOnlyList<RuntimeListener> listeners)
    {
        return listeners
            .Select(static listener => new ProxyConfiguredListenerSummarySource(
                listener.Enabled,
                listener.Protocols.HasFlag(RuntimeListenerProtocols.Http1),
                listener.Protocols.HasFlag(RuntimeListenerProtocols.Http2),
                listener.Http3.EnabledForTraffic))
            .ToArray();
    }
}

public static class ProxyRuntimeListenerSummarySourceMapper
{
    public static IReadOnlyList<ProxyRuntimeListenerSummarySource> FromRuntimeSummary(
        ProxyStatusRuntimeSummary runtime)
    {
        return runtime.Listeners
            .Select(static listener => new ProxyRuntimeListenerSummarySource(
                string.Equals(listener.Kind, "quic", StringComparison.OrdinalIgnoreCase),
                listener.State))
            .ToArray();
    }
}

public static class ProxyRouteSummarySourceMapper
{
    public static IReadOnlyList<ProxyRouteSummarySource> FromRoutes(IReadOnlyList<RuntimeRoute> routes)
    {
        return routes
            .Select(static route => new ProxyRouteSummarySource(
                route.SiteName,
                route.Action == RuntimeRouteAction.Proxy,
                route.Cache.Enabled,
                route.Upstreams.Any(static upstream => RuntimeUpstreamProtocol.IsHttp3(upstream.Protocol))))
            .ToArray();
    }
}

public static class ProxyCertificateSummarySourceMapper
{
    public static ProxyCertificateSummarySource FromConfiguration(
        IReadOnlyList<RuntimeListener> listeners,
        IReadOnlyDictionary<string, RuntimeCertificate> certificates)
    {
        List<string> referenced = [];
        foreach (var listener in listeners)
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
            certificates.Values
                .Select(static certificate => new ProxyCertificateValiditySource(
                    certificate.Id,
                    certificate.Certificate.NotBefore,
                    certificate.Certificate.NotAfter))
                .ToArray());
    }
}

public static class ProxyAcmeSummaryConfigurationSourceMapper
{
    public static ProxyAcmeSummaryConfigurationSource FromConfiguration(RuntimeAcmeOptions acme)
    {
        return new ProxyAcmeSummaryConfigurationSource(
            acme.Enabled,
            acme.Certificates.Count(static certificate => certificate.Enabled));
    }
}

public static class ProxyUpstreamSummarySourceMapper
{
    public static IReadOnlyList<ProxyUpstreamSummarySource> FromStatusResponses(
        IReadOnlyList<ProxyUpstreamStatus> upstreams)
    {
        return upstreams
            .Select(static upstream => new ProxyUpstreamSummarySource(
                upstream.HealthState,
                upstream.HealthCheckEnabled,
                upstream.CircuitBreaker.Enabled,
                upstream.CircuitBreaker.State))
            .ToArray();
    }
}

public static class ProxyLimitSummarySourceMapper
{
    public static ProxyLimitConfigurationSummarySource FromConfiguration(RuntimeLimits limits)
    {
        return new ProxyLimitConfigurationSummarySource(
            limits.MaxActiveClientConnections,
            limits.MaxConcurrentTlsHandshakes,
            limits.RequestsPerMinutePerIp);
    }

    public static ProxyLimitRuntimeSummarySource FromMetrics(ProxyMetricsSnapshot metrics)
    {
        return new ProxyLimitRuntimeSummarySource(
            metrics.ActiveConnections,
            metrics.Tls.ActiveHandshakes,
            metrics.ActiveHttp2Streams,
            metrics.Http3.ActiveStreams,
            metrics.UpstreamHttp3.ActiveStreams);
    }
}

public static class ProxyLogSummarySourceMapper
{
    public static ProxyLogSummarySource FromStatus(ProxyLogPersistenceStatus logPersistence)
    {
        return new ProxyLogSummarySource(
            logPersistence.AccessLogEnabled,
            logPersistence.AdminAuditEnabled,
            logPersistence.State,
            logPersistence.Reason);
    }
}

public static class ProxyShutdownSummarySourceMapper
{
    public static ProxyShutdownSummarySource FromRuntimeSummary(ProxyStatusRuntimeSummary runtime)
    {
        return new ProxyShutdownSummarySource(
            runtime.ListenerLive,
            runtime.IsShuttingDown,
            runtime.ShutdownStartedAtUtc,
            runtime.ShutdownDeadlineUtc);
    }
}
