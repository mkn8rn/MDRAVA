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
        IReadOnlyList<string> sourceFiles,
        ProxyConfigurationDiscovery discovery)
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
            .Select(route => ToRuntimeRoute(route, operationalOptions))
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

        var forwardedHeaders = new RuntimeForwardedHeadersOptions(
            operationalOptions.ForwardedHeaders.Enabled,
            operationalOptions.ForwardedHeaders.TrustedProxies
                .Select(static entry => RuntimeTrustedProxy.TryParse(entry, out var trustedProxy) ? trustedProxy : null)
                .Where(static trustedProxy => trustedProxy is not null)
                .Select(static trustedProxy => trustedProxy!)
                .ToArray());

        return new ProxyConfigurationSnapshot(version, loadedAtUtc, sourceDirectory, sourceFiles, discovery, timeouts, connectionLimits, observability, limits, forwardedHeaders, certificates, listeners, routes);
    }

    public static ProxyConfigurationProjection ToProjection(ProxyConfigurationSnapshot snapshot)
    {
        return new ProxyConfigurationProjection(
            snapshot.Version,
            snapshot.LoadedAtUtc,
            snapshot.SourceDirectory,
            snapshot.SourceFiles,
            snapshot.Discovery,
            snapshot.Timeouts,
            snapshot.ConnectionLimits,
            snapshot.Observability,
            snapshot.Limits,
            snapshot.ForwardedHeaders,
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

    private static RuntimeRoute ToRuntimeRoute(ProxyRouteOptions route, ProxyOperationalOptions operationalOptions)
    {
        var action = ParseAction(route.Action);
        return new RuntimeRoute(
            route.Name,
            route.Host,
            route.PathPrefix,
            action,
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
                .ToArray(),
            new RuntimeHttpsRedirectPolicy(
                route.HttpsRedirect.Enabled ?? false,
                route.HttpsRedirect.StatusCode ?? 308,
                route.HttpsRedirect.HttpsPort),
            new RuntimeCanonicalHostPolicy(
                route.CanonicalHost.Enabled ?? !string.IsNullOrWhiteSpace(route.CanonicalHost.TargetHost),
                route.CanonicalHost.TargetHost,
                route.CanonicalHost.StatusCode ?? 308),
            new RuntimeHeaderPolicy(
                route.HeaderPolicy.SetRequestHeaders
                    .Select(static header => new Http1HeaderField(header.Name, header.Value))
                    .ToArray(),
                route.HeaderPolicy.RemoveRequestHeaders.ToArray(),
                route.HeaderPolicy.SetResponseHeaders
                    .Select(static header => new Http1HeaderField(header.Name, header.Value))
                    .ToArray(),
                route.HeaderPolicy.RemoveResponseHeaders.ToArray()),
            new RuntimePathRewritePolicy(
                route.PathRewrite.StripPrefix,
                route.PathRewrite.ReplacePrefix,
                route.PathRewrite.Replacement),
            new RuntimeRedirectPolicy(
                route.Redirect.StatusCode ?? 308,
                route.Redirect.TargetUrl,
                route.Redirect.TargetPath,
                route.Redirect.PreserveQuery),
            new RuntimeStaticResponse(
                route.StaticResponse.StatusCode,
                route.StaticResponse.ContentType,
                route.StaticResponse.Body),
            new RuntimeMaintenancePolicy(
                route.Maintenance.Enabled ?? false,
                route.Maintenance.RetryAfterSeconds,
                route.Maintenance.ContentType,
                route.Maintenance.Body),
            new RuntimeRouteResolvedOptions(
                route.Overrides.MaxRequestBodyBytes ?? operationalOptions.Limits.MaxRequestBodyBytes,
                TimeSpan.FromMilliseconds(route.Overrides.ClientRequestHeadTimeoutMs ?? operationalOptions.Timeouts.ClientRequestHeadTimeoutMs),
                TimeSpan.FromMilliseconds(route.Overrides.UpstreamResponseHeadTimeoutMs ?? operationalOptions.Timeouts.UpstreamResponseHeadTimeoutMs),
                route.Overrides.AccessLogEnabled ?? operationalOptions.Observability.AccessLogEnabled));
    }

    private static RuntimeRouteAction ParseAction(string action)
    {
        if (string.Equals(action, "redirect", StringComparison.OrdinalIgnoreCase))
        {
            return RuntimeRouteAction.Redirect;
        }

        if (string.Equals(action, "staticResponse", StringComparison.OrdinalIgnoreCase))
        {
            return RuntimeRouteAction.StaticResponse;
        }

        return RuntimeRouteAction.Proxy;
    }

    private static RuntimeListenerTransport ParseTransport(string transport)
    {
        return string.Equals(transport, "https", StringComparison.OrdinalIgnoreCase)
            ? RuntimeListenerTransport.Https
            : RuntimeListenerTransport.Http;
    }
}
