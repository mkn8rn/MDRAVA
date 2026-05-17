using MDRAVA.API.Proxy.Security;
using MDRAVA.API.Proxy.Http3;

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
                listener.ForwardingBufferBytes)
            {
                Protocols = ParseProtocols(listener.Protocols),
                ExperimentalHttp3 = listener.ExperimentalHttp3,
                Http3Enablement = ResolveHttp3Enablement(listener),
                Http3AltSvc = new RuntimeHttp3AltSvcOptions(
                    listener.Http3AltSvcEnabled,
                    listener.Http3AltSvcMaxAgeSeconds),
                Http2Limits = new RuntimeHttp2Limits(
                    listener.Http2MaxConcurrentStreams,
                    listener.Http2MaxHeaderListBytes,
                    listener.Http2MaxFrameSize)
            })
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

        var adminSecurity = AdminSecurityTokenResolver.ToRuntimeOptions(operationalOptions.Admin);
        var acme = ToRuntimeAcmeOptions(operationalOptions.Acme);
        var metrics = ToRuntimeMetricsOptions(operationalOptions.Metrics);

        return new ProxyConfigurationSnapshot(version, loadedAtUtc, sourceDirectory, sourceFiles, discovery, adminSecurity, acme, timeouts, connectionLimits, observability, limits, forwardedHeaders, certificates, listeners, routes)
        {
            Metrics = metrics
        };
    }

    public static ProxyConfigurationProjection ToProjection(ProxyConfigurationSnapshot snapshot)
    {
        var adminSecurity = new RuntimeAdminSecurityProjection(
            snapshot.AdminSecurity.Urls,
            snapshot.AdminSecurity.RequireAuthentication,
            snapshot.AdminSecurity.HasConfiguredToken,
            SecretRedactor.RedactConfiguredSecret(snapshot.AdminSecurity.HasConfiguredToken),
            snapshot.AdminSecurity.TokenEnvironmentVariable,
                snapshot.AdminSecurity.TokenSource,
                snapshot.AdminSecurity.RecentAuditCapacity);
        var acme = new RuntimeAcmeProjection(
            snapshot.Acme.Enabled,
            snapshot.Acme.UseStaging,
            snapshot.Acme.DirectoryUrl,
            snapshot.Acme.ContactEmails,
            snapshot.Acme.TermsAccepted,
            snapshot.Acme.StoragePath,
            snapshot.Acme.RenewBeforeDays,
            snapshot.Acme.CheckIntervalMinutes,
            snapshot.Acme.RetryAfterMinutes,
            snapshot.Acme.Certificates);

        return new ProxyConfigurationProjection(
            snapshot.Version,
            snapshot.LoadedAtUtc,
            snapshot.SourceDirectory,
            snapshot.SourceFiles,
            snapshot.Discovery,
            adminSecurity,
            acme,
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
                    certificate.Source,
                    certificate.Domains ?? [],
                    certificate.HasConfiguredPassword,
                    certificate.Certificate.Subject,
                    certificate.Certificate.Thumbprint,
                    certificate.Certificate.NotBefore,
                    certificate.Certificate.NotAfter))
                .OrderBy(static certificate => certificate.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            snapshot.Listeners,
            snapshot.Routes)
        {
            Metrics = snapshot.Metrics,
            Http3 = Http3RuntimeSupport.Project(snapshot.Listeners)
        };
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
        if (!string.IsNullOrWhiteSpace(options.DirectoryUrl))
        {
            return options.DirectoryUrl.Trim();
        }

        return options.UseStaging
            ? "https://acme-staging-v02.api.letsencrypt.org/directory"
            : "https://acme-v02.api.letsencrypt.org/directory";
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
                    string.IsNullOrWhiteSpace(upstream.Scheme) ? "http" : upstream.Scheme.ToLowerInvariant(),
                    string.IsNullOrWhiteSpace(upstream.Protocol) ? RuntimeUpstreamProtocol.Http1 : upstream.Protocol.Trim().ToLowerInvariant(),
                    upstream.Address,
                    upstream.Port,
                    upstream.Weight,
                    new RuntimeUpstreamTlsOptions(
                        upstream.UpstreamTls.ValidateCertificate,
                        string.IsNullOrWhiteSpace(upstream.UpstreamTls.SniHost) ? null : upstream.UpstreamTls.SniHost.Trim()))
                    {
                        CircuitBreaker = ToRuntimeCircuitBreaker(upstream.CircuitBreaker)
                    })
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
            new RuntimeCachePolicy(
                route.Cache.Enabled,
                route.Cache.MaxEntryBytes,
                route.Cache.MaxTotalBytes,
                TimeSpan.FromSeconds(route.Cache.DefaultTtlSeconds),
                route.Cache.RespectOriginCacheControl,
                route.Cache.VaryByHeaders
                    .Where(static header => !string.IsNullOrWhiteSpace(header))
                    .Select(static header => header.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                route.Cache.CacheableStatusCodes
                    .Distinct()
                    .Order()
                    .ToArray(),
                route.Cache.Methods
                    .Where(static method => !string.IsNullOrWhiteSpace(method))
                    .Select(static method => method.Trim().ToUpperInvariant())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()),
            new RuntimeRouteResolvedOptions(
                route.Overrides.MaxRequestBodyBytes ?? operationalOptions.Limits.MaxRequestBodyBytes,
                TimeSpan.FromMilliseconds(route.Overrides.ClientRequestHeadTimeoutMs ?? operationalOptions.Timeouts.ClientRequestHeadTimeoutMs),
                TimeSpan.FromMilliseconds(route.Overrides.UpstreamResponseHeadTimeoutMs ?? operationalOptions.Timeouts.UpstreamResponseHeadTimeoutMs),
                route.Overrides.AccessLogEnabled ?? operationalOptions.Observability.AccessLogEnabled))
        {
            SiteName = route.SiteName,
            Retry = ToRuntimeRetry(route.Retry)
        };
    }

    private static RuntimeRetryPolicy ToRuntimeRetry(ProxyRetryPolicyOptions retry)
    {
        return new RuntimeRetryPolicy(
            retry.Enabled,
            Math.Max(1, retry.MaxAttempts),
            retry.PerAttemptTimeoutMs.HasValue && retry.PerAttemptTimeoutMs.Value > 0
                ? TimeSpan.FromMilliseconds(retry.PerAttemptTimeoutMs.Value)
                : null,
            retry.RetryOnConnectFailure,
            retry.RetryOnUpstreamResponseHeadTimeout,
            retry.RetryOnStatusCodes
                .Distinct()
                .Order()
                .ToArray(),
            retry.RetryMethods
                .Where(static method => !string.IsNullOrWhiteSpace(method))
                .Select(static method => method.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            TimeSpan.FromMilliseconds(Math.Max(0, retry.RetryBackoffMilliseconds)));
    }

    private static RuntimeCircuitBreakerPolicy ToRuntimeCircuitBreaker(ProxyCircuitBreakerOptions circuitBreaker)
    {
        return new RuntimeCircuitBreakerPolicy(
            circuitBreaker.Enabled,
            circuitBreaker.FailureThreshold,
            TimeSpan.FromSeconds(circuitBreaker.SamplingWindowSeconds),
            TimeSpan.FromSeconds(circuitBreaker.OpenDurationSeconds),
            circuitBreaker.HalfOpenMaxAttempts,
            circuitBreaker.FailureStatusCodes
                .Distinct()
                .Order()
                .ToArray());
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

    private static RuntimeListenerProtocols ParseProtocols(string protocols)
    {
        return protocols.Trim().ToLowerInvariant() switch
        {
            "http2" => RuntimeListenerProtocols.Http2,
            "http1andhttp2" => RuntimeListenerProtocols.Http1AndHttp2,
            "http3preview" => RuntimeListenerProtocols.Http3Preview,
            "http1andhttp3preview" => RuntimeListenerProtocols.Http1AndHttp3Preview,
            "http2andhttp3preview" => RuntimeListenerProtocols.Http2AndHttp3Preview,
            "http1andhttp2andhttp3preview" => RuntimeListenerProtocols.Http1AndHttp2AndHttp3Preview,
            _ => RuntimeListenerProtocols.Http1
        };
    }

    private static RuntimeHttp3Enablement ResolveHttp3Enablement(ListenerOptions listener)
    {
        if (!string.IsNullOrWhiteSpace(listener.Http3Enablement))
        {
            return listener.Http3Enablement.Trim().ToLowerInvariant() switch
            {
                "preview" => RuntimeHttp3Enablement.Preview,
                "beta" => RuntimeHttp3Enablement.Beta,
                _ => RuntimeHttp3Enablement.Disabled
            };
        }

        return ParseProtocols(listener.Protocols).HasHttp3Preview() && listener.ExperimentalHttp3
            ? RuntimeHttp3Enablement.Preview
            : RuntimeHttp3Enablement.Disabled;
    }
}
