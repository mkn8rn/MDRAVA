namespace MDRAVA.API.Proxy.Configuration.Runtime;

public static class ProxyConfigurationMapper
{
    public static ProxyConfigurationSnapshot ToRuntimeSnapshot(
        ProxyOptions options,
        ProxyOperationalOptions operationalOptions,
        IReadOnlyDictionary<string, RuntimeCertificate> certificates,
        int version,
        DateTimeOffset loadedAtUtc,
        string sourceDirectory,
        IReadOnlyList<string> sourceFiles)
    {
        var listeners = options.Listeners
            .Select(static listener => new RuntimeListener(
                listener.Name,
                listener.Address,
                listener.Port,
                listener.Enabled,
                ParseTransport(listener.Transport),
                string.IsNullOrWhiteSpace(listener.DefaultCertificateId) ? null : listener.DefaultCertificateId,
                listener.SniCertificates
                    .Select(static binding => new RuntimeSniCertificateBinding(
                        binding.HostName,
                        binding.CertificateId))
                    .ToArray(),
                listener.Backlog,
                listener.MaxRequestHeadBytes,
                listener.MaxResponseHeadBytes,
                listener.MaxChunkLineBytes,
                listener.ForwardingBufferBytes))
            .ToArray();

        var routes = options.Routes
            .Select(static route => new RuntimeRoute(
                route.Name,
                route.Host,
                route.PathPrefix,
                string.IsNullOrWhiteSpace(route.LoadBalancingPolicy) ? "round-robin" : route.LoadBalancingPolicy,
                new RuntimeHealthCheckOptions(
                    route.HealthCheck.Enabled,
                    string.IsNullOrWhiteSpace(route.HealthCheck.Path) ? "/health" : route.HealthCheck.Path,
                    TimeSpan.FromSeconds(route.HealthCheck.IntervalSeconds),
                    TimeSpan.FromSeconds(route.HealthCheck.TimeoutSeconds),
                    route.HealthCheck.HealthyThreshold,
                    route.HealthCheck.UnhealthyThreshold),
                route.Upstreams
                    .Select(upstream => new RuntimeUpstream(
                        route.Name,
                        upstream.Name,
                        upstream.Address,
                        upstream.Port,
                        upstream.Weight))
                    .ToArray()))
            .ToArray();

        var timeouts = new RuntimeTimeouts(
            TimeSpan.FromMilliseconds(operationalOptions.Timeouts.ClientRequestHeadTimeoutMs),
            TimeSpan.FromMilliseconds(operationalOptions.Timeouts.ClientRequestBodyIdleTimeoutMs),
            TimeSpan.FromMilliseconds(operationalOptions.Timeouts.UpstreamConnectTimeoutMs),
            TimeSpan.FromMilliseconds(operationalOptions.Timeouts.UpstreamResponseHeadTimeoutMs),
            TimeSpan.FromMilliseconds(operationalOptions.Timeouts.UpstreamResponseBodyIdleTimeoutMs),
            TimeSpan.FromMilliseconds(operationalOptions.Timeouts.DownstreamWriteTimeoutMs),
            TimeSpan.FromMilliseconds(operationalOptions.Timeouts.TlsHandshakeTimeoutMs),
            TimeSpan.FromMilliseconds(operationalOptions.Timeouts.ClientKeepAliveIdleTimeoutMs),
            TimeSpan.FromMilliseconds(operationalOptions.Timeouts.UpstreamIdleConnectionLifetimeMs),
            TimeSpan.FromMilliseconds(operationalOptions.Timeouts.TunnelIdleTimeoutMs));

        var connectionLimits = new RuntimeConnectionLimits(
            operationalOptions.Connections.MaxRequestsPerClientConnection,
            operationalOptions.Connections.MaxIdleUpstreamConnectionsPerUpstream,
            operationalOptions.Connections.MaxActiveUpgradedTunnels);

        var observability = new RuntimeObservabilityOptions(
            operationalOptions.Observability.AccessLogEnabled,
            operationalOptions.Observability.RecentDiagnosticsCapacity);

        var limits = new RuntimeLimits(
            operationalOptions.Limits.MaxActiveClientConnections,
            operationalOptions.Limits.MaxConcurrentTlsHandshakes,
            operationalOptions.Limits.RequestsPerMinutePerIp,
            operationalOptions.Limits.UpgradeRequestsPerMinutePerIp,
            operationalOptions.Limits.MaxRequestHeadBytes,
            operationalOptions.Limits.MaxHeaderCount,
            operationalOptions.Limits.MaxHeaderLineBytes,
            operationalOptions.Limits.MaxRequestBodyBytes,
            operationalOptions.Limits.MaxPathBytes,
            TimeSpan.FromSeconds(operationalOptions.Limits.ShutdownGracePeriodSeconds));

        return new ProxyConfigurationSnapshot(version, loadedAtUtc, sourceDirectory, sourceFiles, timeouts, connectionLimits, observability, limits, certificates, listeners, routes);
    }

    public static ProxyConfigurationProjection ToProjection(ProxyConfigurationSnapshot snapshot)
    {
        return new ProxyConfigurationProjection(
            snapshot.Version,
            snapshot.LoadedAtUtc,
            snapshot.SourceDirectory,
            snapshot.SourceFiles,
            snapshot.Timeouts,
            snapshot.ConnectionLimits,
            snapshot.Observability,
            snapshot.Limits,
            snapshot.Certificates.Values
                .Select(static certificate => new RuntimeCertificateProjection(
                    certificate.Id,
                    certificate.Path,
                    certificate.Format,
                    certificate.HasConfiguredPassword,
                    certificate.Certificate.Subject,
                    certificate.Certificate.Thumbprint,
                    certificate.Certificate.NotBefore,
                    certificate.Certificate.NotAfter))
                .OrderBy(static certificate => certificate.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            snapshot.Listeners,
            snapshot.Routes);
    }

    private static RuntimeListenerTransport ParseTransport(string transport)
    {
        return string.Equals(transport, "https", StringComparison.OrdinalIgnoreCase)
            ? RuntimeListenerTransport.Https
            : RuntimeListenerTransport.Http;
    }
}
