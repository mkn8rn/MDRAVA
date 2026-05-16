using System.Net;

namespace MDRAVA.API.Proxy.Hosting;

public sealed class ProxyRuntimeState
{
    private int _isRunning;
    private string? _listenerName;
    private string? _endpoint;
    private DateTimeOffset? _startedAt;
    private DateTimeOffset? _stoppedAt;
    private string? _lastError;

    public ProxyRuntimeSnapshot Snapshot()
    {
        return new ProxyRuntimeSnapshot(
            Volatile.Read(ref _isRunning) == 1,
            _listenerName,
            _endpoint,
            _startedAt,
            _stoppedAt,
            _lastError);
    }

    public void MarkRunning(string listenerName, EndPoint endpoint)
    {
        _listenerName = listenerName;
        _endpoint = endpoint.ToString();
        _startedAt = DateTimeOffset.UtcNow;
        _stoppedAt = null;
        _lastError = null;
        Volatile.Write(ref _isRunning, 1);
    }

    public void MarkStopped(string? error = null)
    {
        _stoppedAt = DateTimeOffset.UtcNow;
        _lastError = error;
        Volatile.Write(ref _isRunning, 0);
    }
}
