using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.HealthChecks;
using MDRAVA.BLL.ControlPlane.Http3;

namespace MDRAVA.BLL.ControlPlane.Status;

public static class ProxyStatusConfigurationSourceMapper
{
    public static ProxyStatusConfigurationSourceSet FromSources(
        int version,
        DateTimeOffset loadedAtUtc,
        IEnumerable<RuntimeListener> listeners,
        IEnumerable<RuntimeRoute> routes,
        IEnumerable<RuntimeCertificate> certificates,
        RuntimeAcmeOptions acme,
        RuntimeLimits limits,
        IEnumerable<ProxyUpstreamHealthSource> upstreamHealthSources)
    {
        ArgumentNullException.ThrowIfNull(listeners);
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(certificates);
        ArgumentNullException.ThrowIfNull(acme);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(upstreamHealthSources);

        var listenerSources = ProxyStatusList.Copy(listeners);
        var routeSources = ProxyStatusList.Copy(routes);

        return new ProxyStatusConfigurationSourceSet(
            ProxyStatusConfigurationSummaryMapper.FromCounts(
                version,
                loadedAtUtc,
                listenerSources.Count,
                routeSources.Count),
            upstreamHealthSources,
            ProxyHttp3SupportConfigurationSourceMapper.FromConfiguration(listenerSources, routeSources),
            ProxyStatusReadinessConfigurationSourceMapper.FromConfiguration(
                version,
                loadedAtUtc,
                listenerSources,
                routeSources,
                certificates,
                acme,
                limits));
    }
}

public static class ProxyStatusReadinessConfigurationSourceMapper
{
    public static ProxyStatusReadinessConfigurationSourceSet FromConfiguration(
        int version,
        DateTimeOffset loadedAtUtc,
        IEnumerable<RuntimeListener> listeners,
        IEnumerable<RuntimeRoute> routes,
        IEnumerable<RuntimeCertificate> certificates,
        RuntimeAcmeOptions acme,
        RuntimeLimits limits)
    {
        ArgumentNullException.ThrowIfNull(listeners);
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(certificates);
        ArgumentNullException.ThrowIfNull(acme);
        ArgumentNullException.ThrowIfNull(limits);

        var listenerSources = ProxyStatusList.Copy(listeners);
        var routeSources = ProxyStatusList.Copy(routes);

        return new ProxyStatusReadinessConfigurationSourceSet(
            true,
            version,
            loadedAtUtc,
            ProxyConfiguredListenerSummarySourceMapper.FromListeners(listenerSources),
            ProxyRouteSummarySourceMapper.FromRoutes(routeSources),
            ProxyCertificateSummarySourceMapper.FromConfiguration(
                listenerSources,
                certificates),
            ProxyAcmeSummaryConfigurationSourceMapper.FromConfiguration(acme),
            ProxyLimitConfigurationSummarySourceMapper.FromConfiguration(limits));
    }
}

public static class ProxyConfiguredListenerSummarySourceMapper
{
    public static IReadOnlyList<ProxyConfiguredListenerSummarySource> FromListeners(
        IEnumerable<RuntimeListener> listeners)
    {
        return ProxyStatusList.Copy(listeners
            .Select(static listener => new ProxyConfiguredListenerSummarySource(
                listener.Enabled,
                listener.Protocols.HasFlag(RuntimeListenerProtocols.Http1),
                listener.Protocols.HasFlag(RuntimeListenerProtocols.Http2),
                listener.Http3.EnabledForTraffic)));
    }
}

public static class ProxyRouteSummarySourceMapper
{
    public static IReadOnlyList<ProxyRouteSummarySource> FromRoutes(IEnumerable<RuntimeRoute> routes)
    {
        return ProxyStatusList.Copy(routes
            .Select(static route => new ProxyRouteSummarySource(
                route.SiteName,
                route.Action == RuntimeRouteAction.Proxy,
                route.Cache.Enabled,
                route.Upstreams.Any(static upstream => RuntimeUpstreamProtocol.IsHttp3(upstream.Protocol)))));
    }
}

public static class ProxyCertificateSummarySourceMapper
{
    public static ProxyCertificateSummarySource FromConfiguration(
        IEnumerable<RuntimeListener> listeners,
        IEnumerable<RuntimeCertificate> certificates)
    {
        ArgumentNullException.ThrowIfNull(listeners);
        ArgumentNullException.ThrowIfNull(certificates);

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
            certificates
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

public static class ProxyLimitConfigurationSummarySourceMapper
{
    public static ProxyLimitConfigurationSummarySource FromConfiguration(RuntimeLimits limits)
    {
        return new ProxyLimitConfigurationSummarySource(
            limits.MaxActiveClientConnections,
            limits.MaxConcurrentTlsHandshakes,
            limits.RequestsPerMinutePerIp);
    }
}
