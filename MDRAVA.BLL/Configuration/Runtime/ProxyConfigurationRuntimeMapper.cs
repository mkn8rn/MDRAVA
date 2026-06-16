namespace MDRAVA.BLL.Configuration;

public static partial class ProxyConfigurationRuntimeMapper
{
    public static ProxyConfigurationSnapshot ToRuntimeSnapshot(
        ProxyOptions options,
        ProxyOperationalOptions operationalOptions,
        ProxyAdminTokenResolution adminTokenResolution,
        IReadOnlyDictionary<string, RuntimeCertificate> certificates,
        int version,
        DateTimeOffset loadedAtUtc,
        string sourceDirectory,
        IReadOnlyList<string> sourceFiles,
        ProxyConfigurationDiscovery discovery)
    {
        var listeners = ToRuntimeListeners(options.Listeners);

        var routes = ToRuntimeRoutes(options.Routes, operationalOptions);

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
            operationalOptions.Observability.RecentDiagnosticsCapacity,
            new RuntimeLogPersistenceOptions(
                operationalOptions.Observability.LogPersistence.AccessLogEnabled,
                operationalOptions.Observability.LogPersistence.AdminAuditEnabled,
                operationalOptions.Observability.LogPersistence.MaxFileBytes,
                operationalOptions.Observability.LogPersistence.MaxFiles));

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
                .Where(static entry => !string.IsNullOrWhiteSpace(entry))
                .Select(static entry => entry.Trim())
                .ToArray());

        var adminSecurity = ToRuntimeAdminSecurityOptions(operationalOptions.Admin, adminTokenResolution);
        var acme = ToRuntimeAcmeOptions(operationalOptions.Acme);
        var metrics = ToRuntimeMetricsOptions(operationalOptions.Metrics);

        return new ProxyConfigurationSnapshot(version, loadedAtUtc, sourceDirectory, sourceFiles, discovery, adminSecurity, acme, timeouts, connectionLimits, observability, limits, forwardedHeaders, certificates, listeners, routes)
        {
            Metrics = metrics
        };
    }

    public static IReadOnlyList<RuntimeListener> ToRuntimeListeners(IReadOnlyList<ListenerOptions> listeners)
    {
        ArgumentNullException.ThrowIfNull(listeners);

        return listeners
            .Select(static listener =>
            {
                var http3Compatibility = RuntimeHttp3Compatibility.From(listener);
                return new RuntimeListener(
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
                    listener.ForwardingBufferBytes,
                    http3Compatibility.Protocols,
                    http3Compatibility.EffectiveEnablement,
                    new RuntimeHttp3AltSvcOptions(
                        listener.Http3AltSvcEnabled,
                        listener.Http3AltSvcMaxAgeSeconds),
                    new RuntimeHttp2Limits(
                        listener.Http2MaxConcurrentStreams,
                        listener.Http2MaxHeaderListBytes,
                        listener.Http2MaxFrameSize));
            })
            .ToArray();
    }

    public static IReadOnlyList<RuntimeRoute> ToRuntimeRoutes(
        IReadOnlyList<ProxyRouteOptions> routes,
        ProxyOperationalOptions operationalOptions)
    {
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(operationalOptions);

        return routes
            .Select(route => ToRuntimeRoute(route, operationalOptions))
            .ToArray();
    }

    public static RuntimeAdminSecurityOptions ToRuntimeAdminSecurityOptions(
        ProxyAdminOptions options,
        ProxyAdminTokenResolution resolvedToken)
    {
        return new RuntimeAdminSecurityOptions(
            ProxyAdminSecurityTokenPolicy.NormalizeUrls(options.Urls),
            options.RequireAuthentication,
            !string.IsNullOrEmpty(resolvedToken.Token),
            resolvedToken.Token,
            resolvedToken.TokenEnvironmentVariable,
            resolvedToken.TokenSource,
            options.RecentAuditCapacity);
    }

    public static RuntimeAcmeOptions ToRuntimeAcmeOptions(ProxyAcmeOptions options)
    {
        var directoryUrl = ResolveAcmeDirectoryUrl(options);
        return new RuntimeAcmeOptions(
            options.Enabled,
            options.UseStaging,
            directoryUrl,
            options.ContactEmails
                .Where(static contact => !string.IsNullOrWhiteSpace(contact))
                .Select(static contact => contact.Trim())
                .ToArray(),
            options.TermsAccepted,
            string.IsNullOrWhiteSpace(options.StoragePath) ? "acme" : options.StoragePath.Trim(),
            options.RenewBeforeDays,
            options.CheckIntervalMinutes,
            options.RetryAfterMinutes,
            options.Certificates
                .Select(certificate => new RuntimeAcmeCertificateOptions(
                    certificate.Id,
                    certificate.Enabled,
                    certificate.Domains
                        .Where(static domain => !string.IsNullOrWhiteSpace(domain))
                        .Select(static domain => domain.Trim().ToLowerInvariant())
                        .ToArray(),
                    certificate.RenewBeforeDays ?? options.RenewBeforeDays))
                .ToArray());
    }

    public static RuntimeMetricsOptions ToRuntimeMetricsOptions(ProxyMetricsOptions options)
    {
        return new RuntimeMetricsOptions(
            options.Enabled,
            RuntimeMetricsOptions.FixedAdminEndpointPath,
            ProtectedByAdminAuth: true,
            options.IncludePerRouteLabels,
            options.IncludePerUpstreamLabels,
            options.PublicMetricsEnabled);
    }

    public static string ResolveAcmeDirectoryUrl(ProxyAcmeOptions options)
    {
        return ProxyAcmeDirectoryPolicy.ResolveDirectoryUrl(options);
    }

    private static RuntimeListenerTransport ParseTransport(string transport)
    {
        return string.Equals(transport, "https", StringComparison.OrdinalIgnoreCase)
            ? RuntimeListenerTransport.Https
            : RuntimeListenerTransport.Http;
    }

}
