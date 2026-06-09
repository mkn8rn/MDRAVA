namespace MDRAVA.BLL.ControlPlane;

public static class ProxyStatusResponseBuilder
{
    public static ProxyStatusResponse Build(ProxyStatusInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var configuration = input.Configuration;
        var runtime = input.Runtime;
        var listenerCount = configuration?.Listeners.Count ?? 0;
        var routeCount = configuration?.Routes.Count ?? 0;
        var (readiness, subsystems) = ProxyStatusReadinessBuilder.Build(
            configuration,
            runtime,
            input.Metrics,
            input.Upstreams,
            input.Http3,
            input.LogPersistence,
            input.CacheStatus,
            input.AcmeStatuses,
            input.RuntimePreflight);

        return new ProxyStatusResponse(
            runtime.IsRunning,
            runtime.ListenerName,
            runtime.Endpoint,
            runtime.StartedAt,
            runtime.StoppedAt,
            runtime.LastError,
            runtime.IsShuttingDown,
            runtime.ShutdownStartedAtUtc,
            runtime.ShutdownDeadlineUtc,
            configuration?.Version,
            configuration?.LoadedAtUtc,
            listenerCount,
            routeCount,
            input.Metrics,
            input.Upstreams)
        {
            Listeners = runtime.Listeners,
            LastListenerReload = runtime.LastListenerReload,
            Http3 = input.Http3,
            RouteDiagnostics = RouteDiagnosticsStatus.Enabled,
            ConfigLint = input.ConfigLint,
            LogPersistence = input.LogPersistence,
            Readiness = readiness,
            Subsystems = subsystems,
            RuntimePreflight = input.RuntimePreflight
        };
    }
}
