using System.Collections.ObjectModel;
using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.Caching;
using MDRAVA.BLL.ControlPlane.ConfigLint;
using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.ControlPlane.Listeners;
using MDRAVA.BLL.ControlPlane.Metrics;

namespace MDRAVA.BLL.ControlPlane.Status;

public sealed record ProxyStatusInput
{
    public ProxyStatusInput(
        ProxyStatusRuntimeSummary Runtime,
        ProxyStatusConfigurationSummary? Configuration,
        ProxyMetricsSnapshot Metrics,
        IReadOnlyList<ProxyUpstreamStatus> Upstreams,
        RuntimeHttp3SupportProjection Http3,
        ProxyLogPersistenceStatus LogPersistence,
        ProxyCacheStatus? CacheStatus,
        IReadOnlyList<AcmeCertificateLifecycleStatus> AcmeStatuses,
        ProxyRuntimePreflightStatus RuntimePreflight,
        DateTimeOffset ObservedAtUtc,
        ProxyStatusReadinessInput Readiness,
        ConfigLintStatus ConfigLint)
    {
        ArgumentNullException.ThrowIfNull(Runtime);
        ArgumentNullException.ThrowIfNull(Upstreams);
        ArgumentNullException.ThrowIfNull(AcmeStatuses);
        ArgumentNullException.ThrowIfNull(Readiness);

        this.Runtime = Runtime;
        this.Configuration = Configuration;
        this.Metrics = Metrics;
        this.Upstreams = ProxyStatusList.Copy(Upstreams);
        this.Http3 = Http3;
        this.LogPersistence = LogPersistence;
        this.CacheStatus = CacheStatus;
        this.AcmeStatuses = ProxyStatusList.Copy(AcmeStatuses);
        this.RuntimePreflight = RuntimePreflight;
        this.ObservedAtUtc = ObservedAtUtc;
        this.Readiness = Readiness;
        this.ConfigLint = ConfigLint;
    }

    public ProxyStatusRuntimeSummary Runtime { get; }

    public ProxyStatusConfigurationSummary? Configuration { get; }

    public ProxyMetricsSnapshot Metrics { get; }

    public IReadOnlyList<ProxyUpstreamStatus> Upstreams { get; }

    public RuntimeHttp3SupportProjection Http3 { get; }

    public ProxyLogPersistenceStatus LogPersistence { get; }

    public ProxyCacheStatus? CacheStatus { get; }

    public IReadOnlyList<AcmeCertificateLifecycleStatus> AcmeStatuses { get; }

    public ProxyRuntimePreflightStatus RuntimePreflight { get; }

    public DateTimeOffset ObservedAtUtc { get; }

    public ProxyStatusReadinessInput Readiness { get; }

    public ConfigLintStatus ConfigLint { get; }
}

public sealed record ProxyStatusRuntimeSummary
{
    public ProxyStatusRuntimeSummary(
        bool ListenerLive,
        string? ListenerName,
        string? Endpoint,
        DateTimeOffset? StartedAt,
        DateTimeOffset? StoppedAt,
        string? LastError,
        bool IsShuttingDown,
        DateTimeOffset? ShutdownStartedAtUtc,
        DateTimeOffset? ShutdownDeadlineUtc,
        IReadOnlyList<ProxyListenerStatus> Listeners,
        ProxyListenerReloadResult? LastListenerReload)
    {
        ArgumentNullException.ThrowIfNull(Listeners);

        this.ListenerLive = ListenerLive;
        this.ListenerName = ListenerName;
        this.Endpoint = Endpoint;
        this.StartedAt = StartedAt;
        this.StoppedAt = StoppedAt;
        this.LastError = LastError;
        this.IsShuttingDown = IsShuttingDown;
        this.ShutdownStartedAtUtc = ShutdownStartedAtUtc;
        this.ShutdownDeadlineUtc = ShutdownDeadlineUtc;
        this.Listeners = ProxyStatusList.Copy(Listeners);
        this.LastListenerReload = LastListenerReload;
    }

    public bool ListenerLive { get; }

    public string? ListenerName { get; }

    public string? Endpoint { get; }

    public DateTimeOffset? StartedAt { get; }

    public DateTimeOffset? StoppedAt { get; }

    public string? LastError { get; }

    public bool IsShuttingDown { get; }

    public DateTimeOffset? ShutdownStartedAtUtc { get; }

    public DateTimeOffset? ShutdownDeadlineUtc { get; }

    public IReadOnlyList<ProxyListenerStatus> Listeners { get; }

    public ProxyListenerReloadResult? LastListenerReload { get; }
}

public static class ProxyStatusRuntimeSummaryMapper
{
    public static ProxyStatusRuntimeSummary FromSources(
        bool listenerLive,
        string? listenerName,
        string? endpoint,
        DateTimeOffset? startedAt,
        DateTimeOffset? stoppedAt,
        string? lastError,
        bool isShuttingDown,
        DateTimeOffset? shutdownStartedAtUtc,
        DateTimeOffset? shutdownDeadlineUtc,
        IReadOnlyList<ProxyListenerStatus> listeners,
        ProxyListenerReloadResult? lastListenerReload)
    {
        return new ProxyStatusRuntimeSummary(
            listenerLive,
            listenerName,
            endpoint,
            startedAt,
            stoppedAt,
            lastError,
            isShuttingDown,
            shutdownStartedAtUtc,
            shutdownDeadlineUtc,
            listeners,
            lastListenerReload);
    }
}

public sealed record ProxyStatusConfigurationSummary(
    int Version,
    DateTimeOffset LoadedAtUtc,
    int ListenerCount,
    int RouteCount);

public static class ProxyStatusConfigurationSummaryMapper
{
    public static ProxyStatusConfigurationSummary FromCounts(
        int version,
        DateTimeOffset loadedAtUtc,
        int listenerCount,
        int routeCount)
    {
        return new ProxyStatusConfigurationSummary(
            version,
            loadedAtUtc,
            listenerCount,
            routeCount);
    }
}

public interface IProxyStatusInputReader
{
    ProxyStatusInput Read();
}

internal static class ProxyStatusList
{
    public static ReadOnlyCollection<T> Copy<T>(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return new ReadOnlyCollection<T>(values.Select(RequireValue).ToArray());
    }

    private static T RequireValue<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value;
    }

    public static IReadOnlyList<string> CopyStrings(
        IEnumerable<string> values,
        string parameterName)
    {
        var copy = Copy(values);
        foreach (var value in copy)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Values cannot be empty.", parameterName);
            }
        }

        return copy;
    }
}

internal static class ProxyStatusFacts
{
    public static void RequireNonNegative(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Values cannot be negative.");
        }
    }

    public static void RequireNonNegative(long value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Values cannot be negative.");
        }
    }
}
