using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.HealthChecks;
using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.ControlPlane.Status;

namespace MDRAVA.INF.Proxy.Status;

public static class ProxyStatusConfigurationSourceMapper
{
    public static ProxyStatusConfigurationSourceSet FromConfiguration(
        ProxyConfigurationSnapshot configuration,
        IReadOnlyList<ProxyUpstreamHealthSource> upstreamHealthSources)
    {
        return new ProxyStatusConfigurationSourceSet(
            ProxyStatusConfigurationSummaryMapper.FromCounts(
                configuration.Version,
                configuration.LoadedAtUtc,
                configuration.Listeners.Count,
                configuration.Routes.Count),
            upstreamHealthSources,
            ProxyHttp3SupportConfigurationSourceMapper.FromConfiguration(configuration.Listeners, configuration.Routes),
            ProxyStatusReadinessConfigurationSourceMapper.FromConfiguration(
                configuration.Version,
                configuration.LoadedAtUtc,
                configuration.Listeners,
                configuration.Routes,
                configuration.Certificates,
                configuration.Acme,
                configuration.Limits));
    }
}

public static class ProxyStatusReadinessConfigurationSourceMapper
{
    public static ProxyStatusReadinessConfigurationSourceSet FromConfiguration(
        int version,
        DateTimeOffset loadedAtUtc,
        IReadOnlyList<RuntimeListener> listeners,
        IReadOnlyList<RuntimeRoute> routes,
        IReadOnlyDictionary<string, RuntimeCertificate> certificates,
        RuntimeAcmeOptions acme,
        RuntimeLimits limits)
    {
        ArgumentNullException.ThrowIfNull(listeners);
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(certificates);
        ArgumentNullException.ThrowIfNull(acme);
        ArgumentNullException.ThrowIfNull(limits);

        return new ProxyStatusReadinessConfigurationSourceSet(
            true,
            version,
            loadedAtUtc,
            ProxyConfiguredListenerSummarySourceMapper.FromListeners(listeners),
            ProxyRouteSummarySourceMapper.FromRoutes(routes),
            ProxyCertificateSummarySourceMapper.FromConfiguration(
                listeners,
                certificates),
            ProxyAcmeSummaryConfigurationSourceMapper.FromConfiguration(acme),
            ProxyLimitConfigurationSummarySourceMapper.FromConfiguration(limits));
    }
}

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
