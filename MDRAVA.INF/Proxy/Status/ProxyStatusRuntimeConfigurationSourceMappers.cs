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
            Http3SupportSourceMapper.FromConfiguration(configuration.Listeners, configuration.Routes),
            ProxyStatusReadinessConfigurationSourceMapper.FromConfiguration(configuration));
    }
}

public static class ProxyStatusReadinessConfigurationSourceMapper
{
    public static ProxyStatusReadinessConfigurationSourceSet FromConfiguration(
        ProxyConfigurationSnapshot? configuration)
    {
        if (configuration is null)
        {
            return ProxyStatusReadinessConfigurationSourceSet.Missing;
        }

        return new ProxyStatusReadinessConfigurationSourceSet(
            true,
            configuration.Version,
            configuration.LoadedAtUtc,
            ProxyConfiguredListenerSummarySourceMapper.FromListeners(configuration.Listeners),
            ProxyRouteSummarySourceMapper.FromRoutes(configuration.Routes),
            ProxyCertificateSummarySourceMapper.FromConfiguration(
                configuration.Listeners,
                configuration.Certificates),
            ProxyAcmeSummaryConfigurationSourceMapper.FromConfiguration(configuration.Acme),
            ProxyLimitConfigurationSummarySourceMapper.FromConfiguration(configuration.Limits));
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
