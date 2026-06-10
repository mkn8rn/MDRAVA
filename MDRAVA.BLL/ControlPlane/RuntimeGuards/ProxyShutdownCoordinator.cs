namespace MDRAVA.BLL.ControlPlane.RuntimeGuards;

public sealed class ProxyShutdownCoordinator : IDisposable
{
    private readonly object _gate = new();
    private CancellationTokenSource? _shutdownCts;
    private int _isShuttingDown;
    private DateTimeOffset? _startedAtUtc;
    private DateTimeOffset? _deadlineUtc;

    public bool IsShuttingDown => Volatile.Read(ref _isShuttingDown) == 1;

    public DateTimeOffset? StartedAtUtc => _startedAtUtc;

    public DateTimeOffset? DeadlineUtc => _deadlineUtc;

    public CancellationToken Token
    {
        get
        {
            lock (_gate)
            {
                return _shutdownCts?.Token ?? CancellationToken.None;
            }
        }
    }

    public CancellationToken BeginShutdown(TimeSpan gracePeriod)
    {
        lock (_gate)
        {
            if (_shutdownCts is not null)
            {
                return _shutdownCts.Token;
            }

            _startedAtUtc = DateTimeOffset.UtcNow;
            _deadlineUtc = _startedAtUtc.Value.Add(gracePeriod);
            Volatile.Write(ref _isShuttingDown, 1);
            _shutdownCts = new CancellationTokenSource(gracePeriod);
            return _shutdownCts.Token;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _shutdownCts?.Dispose();
        }
    }
}
