namespace MDRAVA.API.Proxy.Configuration.Runtime;

public static class ProxyConfigurationMapper
{
    public static ProxyConfigurationSnapshot ToRuntimeSnapshot(
        ProxyOptions options,
        ProxyOperationalOptions operationalOptions,
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
            TimeSpan.FromMilliseconds(operationalOptions.Timeouts.DownstreamWriteTimeoutMs));

        return new ProxyConfigurationSnapshot(version, loadedAtUtc, sourceDirectory, sourceFiles, timeouts, listeners, routes);
    }

    public static ProxyConfigurationProjection ToProjection(ProxyConfigurationSnapshot snapshot)
    {
        return new ProxyConfigurationProjection(
            snapshot.Version,
            snapshot.LoadedAtUtc,
            snapshot.SourceDirectory,
            snapshot.SourceFiles,
            snapshot.Timeouts,
            snapshot.Listeners,
            snapshot.Routes);
    }
}
