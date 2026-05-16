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
                route.Upstreams
                    .Select(static upstream => new RuntimeUpstream(
                        upstream.Name,
                        upstream.Address,
                        upstream.Port))
                    .ToArray()))
            .ToArray();

        var timeouts = new RuntimeTimeouts(
            TimeSpan.FromMilliseconds(operationalOptions.Timeouts.ClientRequestHeadTimeoutMs),
            TimeSpan.FromMilliseconds(operationalOptions.Timeouts.ClientRequestBodyIdleTimeoutMs),
            TimeSpan.FromMilliseconds(operationalOptions.Timeouts.UpstreamConnectTimeoutMs),
            TimeSpan.FromMilliseconds(operationalOptions.Timeouts.UpstreamResponseHeadTimeoutMs),
            TimeSpan.FromMilliseconds(operationalOptions.Timeouts.UpstreamResponseBodyIdleTimeoutMs),
            TimeSpan.FromMilliseconds(operationalOptions.Timeouts.DownstreamWriteTimeoutMs),
            TimeSpan.FromMilliseconds(operationalOptions.Timeouts.TlsHandshakeTimeoutMs));

        return new ProxyConfigurationSnapshot(version, loadedAtUtc, sourceDirectory, sourceFiles, timeouts, certificates, listeners, routes);
    }

    public static ProxyConfigurationProjection ToProjection(ProxyConfigurationSnapshot snapshot)
    {
        return new ProxyConfigurationProjection(
            snapshot.Version,
            snapshot.LoadedAtUtc,
            snapshot.SourceDirectory,
            snapshot.SourceFiles,
            snapshot.Timeouts,
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
