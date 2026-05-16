using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Metrics;

namespace MDRAVA.API.Proxy.Connections;

public sealed class UpstreamConnectionPool : IDisposable
{
    private readonly UpstreamConnectionFactory _connectionFactory;
    private readonly ProxyMetrics _metrics;
    private readonly object _gate = new();
    private readonly Dictionary<string, Queue<PooledUpstreamConnection>> _idleConnections = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public UpstreamConnectionPool(
        UpstreamConnectionFactory connectionFactory,
        ProxyMetrics metrics)
    {
        _connectionFactory = connectionFactory;
        _metrics = metrics;
    }

    public async ValueTask<UpstreamConnectionLease> BorrowAsync(
        RuntimeUpstream upstream,
        RuntimeTimeouts timeouts,
        RuntimeConnectionLimits limits,
        CancellationToken cancellationToken)
    {
        var key = GetKey(upstream);
        PooledUpstreamConnection? connection = null;

        lock (_gate)
        {
            ThrowIfDisposed();
            if (_idleConnections.TryGetValue(key, out var queue))
            {
                while (queue.Count > 0)
                {
                    var candidate = queue.Dequeue();
                    _metrics.UpstreamPoolIdleConnectionDiscarded();
                    if (IsExpired(candidate, timeouts.UpstreamIdleConnectionLifetime))
                    {
                        candidate.Dispose();
                        _metrics.UpstreamConnectionDiscarded();
                        continue;
                    }

                    connection = candidate;
                    break;
                }
            }
        }

        if (connection is not null)
        {
            connection.CanReturnToPool = false;
            connection.MaxIdleConnections = limits.MaxIdleUpstreamConnectionsPerUpstream;
            _metrics.UpstreamConnectionReused();
            _metrics.UpstreamPoolConnectionBorrowed();
            return new UpstreamConnectionLease(this, connection);
        }

        var socket = await _connectionFactory.ConnectAsync(
            upstream,
            timeouts.UpstreamConnectTimeout,
            cancellationToken);
        connection = new PooledUpstreamConnection(key, upstream, socket);
        connection.MaxIdleConnections = limits.MaxIdleUpstreamConnectionsPerUpstream;
        _metrics.UpstreamConnectionOpened();
        _metrics.UpstreamPoolConnectionBorrowed();
        return new UpstreamConnectionLease(this, connection);
    }

    internal void Return(PooledUpstreamConnection connection)
    {
        if (!connection.CanReturnToPool || _disposed)
        {
            connection.Dispose();
            _metrics.UpstreamConnectionDiscarded();
            _metrics.UpstreamPoolConnectionClosedActive();
            return;
        }

        lock (_gate)
        {
            if (_disposed)
            {
                connection.Dispose();
                _metrics.UpstreamConnectionDiscarded();
                _metrics.UpstreamPoolConnectionClosedActive();
                return;
            }

            var queue = GetOrCreateQueue(connection.Key);
            if (queue.Count >= connection.MaxIdleConnections)
            {
                connection.Dispose();
                _metrics.UpstreamConnectionDiscarded();
                _metrics.UpstreamPoolConnectionClosedActive();
                return;
            }

            connection.LastUsedUtc = DateTimeOffset.UtcNow;
            connection.CanReturnToPool = false;
            queue.Enqueue(connection);
            _metrics.UpstreamPoolConnectionReturnedIdle();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var queue in _idleConnections.Values)
            {
                while (queue.Count > 0)
                {
                    var connection = queue.Dequeue();
                    connection.Dispose();
                    _metrics.UpstreamConnectionDiscarded();
                    _metrics.UpstreamPoolIdleConnectionDiscarded();
                }
            }

            _idleConnections.Clear();
        }
    }

    private Queue<PooledUpstreamConnection> GetOrCreateQueue(string key)
    {
        if (!_idleConnections.TryGetValue(key, out var queue))
        {
            queue = new Queue<PooledUpstreamConnection>();
            _idleConnections.Add(key, queue);
        }

        return queue;
    }

    private static bool IsExpired(PooledUpstreamConnection connection, TimeSpan idleLifetime)
    {
        return DateTimeOffset.UtcNow - connection.LastUsedUtc > idleLifetime;
    }

    private static string GetKey(RuntimeUpstream upstream)
    {
        return $"{upstream.Address}:{upstream.Port}";
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(UpstreamConnectionPool));
        }
    }
}
