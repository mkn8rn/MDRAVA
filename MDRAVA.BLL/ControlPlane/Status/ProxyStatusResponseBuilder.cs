using MDRAVA.BLL.ControlPlane.RouteDiagnostics;

namespace MDRAVA.BLL.ControlPlane.Status;

public static class ProxyStatusResponseBuilder
{
    public static ProxyStatusResponse Build(ProxyStatusInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var configuration = input.Configuration;
        var runtime = input.Runtime;
        var listenerCount = configuration?.ListenerCount ?? 0;
        var routeCount = configuration?.RouteCount ?? 0;
        var (readiness, subsystems) = ProxyStatusReadinessBuilder.Build(input.Readiness);

        return new ProxyStatusResponse(
            runtime.ListenerLive,
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
