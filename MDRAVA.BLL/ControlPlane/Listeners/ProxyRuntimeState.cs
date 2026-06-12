using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.ControlPlane.Status;

namespace MDRAVA.BLL.ControlPlane.Listeners;

public sealed class ProxyRuntimeState : IProxyStatusRuntimeStateSource, IHttp3AltSvcRuntimeListenerSource
{
    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider;
    private int _isRunning;
    private string? _listenerName;
    private string? _endpoint;
    private DateTimeOffset? _startedAt;
    private DateTimeOffset? _stoppedAt;
    private string? _lastError;
    private int _isShuttingDown;
    private DateTimeOffset? _shutdownStartedAtUtc;
    private DateTimeOffset? _shutdownDeadlineUtc;
    private IReadOnlyList<ProxyListenerStatus> _listeners = [];
    private ProxyListenerReloadResult? _lastListenerReload;

    public ProxyRuntimeState(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public ProxyRuntimeSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new ProxyRuntimeSnapshot(
                Volatile.Read(ref _isRunning) == 1,
                _listenerName,
                _endpoint,
                _startedAt,
                _stoppedAt,
                _lastError,
                Volatile.Read(ref _isShuttingDown) == 1,
                _shutdownStartedAtUtc,
                _shutdownDeadlineUtc)
            {
                Listeners = _listeners,
                LastListenerReload = _lastListenerReload
            };
        }
    }

    public ProxyStatusRuntimeSummary ReadRuntimeSummary()
    {
        return ProxyStatusRuntimeSummaryMapper.FromRuntime(Snapshot());
    }

    public IReadOnlyList<ProxyListenerStatus> ReadRuntimeListeners()
    {
        lock (_gate)
        {
            return _listeners;
        }
    }

    public void MarkShuttingDown(DateTimeOffset startedAtUtc, DateTimeOffset deadlineUtc)
    {
        lock (_gate)
        {
            _shutdownStartedAtUtc = startedAtUtc;
            _shutdownDeadlineUtc = deadlineUtc;
            Volatile.Write(ref _isShuttingDown, 1);
        }
    }

    public void ReplaceListeners(
        IReadOnlyList<ProxyListenerStatus> listeners,
        ProxyListenerReloadResult? lastReload)
    {
        lock (_gate)
        {
            _listeners = listeners;
            _lastListenerReload = lastReload ?? _lastListenerReload;

            var active = listeners.FirstOrDefault(static listener => listener.State == ProxyListenerState.Active);
            if (active is not null)
            {
                _listenerName = active.Name;
                _endpoint = $"{active.Address}:{active.Port}";
                _startedAt = active.StartedAtUtc;
                _stoppedAt = null;
                _lastError = null;
                Volatile.Write(ref _isRunning, 1);
                Volatile.Write(ref _isShuttingDown, 0);
                _shutdownStartedAtUtc = null;
                _shutdownDeadlineUtc = null;
                return;
            }

            _listenerName = null;
            _endpoint = null;
            _startedAt = null;
            _stoppedAt = _timeProvider.GetUtcNow();
            _lastError = listeners.Count == 0
                ? "No configured proxy listener."
                : listeners.FirstOrDefault(static listener => listener.State == ProxyListenerState.Failed)?.LastError;
            Volatile.Write(ref _isRunning, 0);
        }
    }
}
