#pragma warning disable CA1416

namespace MDRAVA.API.Proxy.Http3;

public sealed class Http3UpstreamConnectionPool : IDisposable
{
    private const int DefaultMaxStreamsPerConnection = 8;

    private readonly ProxyMetrics _metrics;
    private readonly object _gate = new();
    private readonly Dictionary<string, SemaphoreSlim> _keyGates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<Http3UpstreamPooledConnection>> _connections = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public Http3UpstreamConnectionPool(ProxyMetrics metrics)
    {
        _metrics = metrics;
    }

    internal async ValueTask<Http3UpstreamConnection> BorrowAsync(
        RuntimeUpstream upstream,
        RuntimeTimeouts timeouts,
        RuntimeConnectionLimits limits,
        int maxFramePayloadBytes,
        CancellationToken cancellationToken)
    {
        var key = GetKey(upstream);
        var gate = GetKeyGate(key);
        await gate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            await PruneExpiredIdleConnectionsAsync(key, timeouts.UpstreamIdleConnectionLifetime);
            if (TryReserveExistingConnection(key, timeouts.UpstreamIdleConnectionLifetime, out var pooled))
            {
                _metrics.UpstreamHttp3PoolConnectionReused();
                return await OpenReservedStreamAsync(
                    key,
                    pooled,
                    timeouts,
                    maxFramePayloadBytes,
                    cancellationToken);
            }

            var maxConnections = MaxConnectionsFor(limits);
            if (ConnectionCount(key) >= maxConnections)
            {
                _metrics.UpstreamHttp3StreamLimitRejected();
                throw new Http3UpstreamProtocolException(
                    "All upstream HTTP/3 pooled connections are saturated.",
                    Http3UpstreamFailureKind.ConnectFailure);
            }

            Http3UpstreamTransport transport;
            try
            {
                transport = await Http3UpstreamConnection.OpenTransportAsync(
                    upstream,
                    timeouts,
                    _metrics,
                    cancellationToken);
            }
            catch
            {
                _metrics.UpstreamHttp3ConnectionFailed();
                throw;
            }

            pooled = new Http3UpstreamPooledConnection(
                key,
                transport,
                _metrics,
                DefaultMaxStreamsPerConnection);
            AddConnection(key, pooled);
            if (!pooled.TryReserveStream(timeouts.UpstreamIdleConnectionLifetime))
            {
                pooled.MarkUnusable();
                _metrics.UpstreamHttp3StreamLimitRejected();
                throw new Http3UpstreamProtocolException(
                    "New upstream HTTP/3 pooled connection could not reserve a stream.",
                    Http3UpstreamFailureKind.ConnectFailure);
            }

            return await OpenReservedStreamAsync(
                key,
                pooled,
                timeouts,
                maxFramePayloadBytes,
                cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask PruneIdleConnectionsAsync(RuntimeUpstream upstream, TimeSpan idleLifetime)
    {
        var key = GetKey(upstream);
        var gate = GetKeyGate(key);
        await gate.WaitAsync();
        try
        {
            await PruneExpiredIdleConnectionsAsync(key, idleLifetime);
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose()
    {
        List<Http3UpstreamPooledConnection> connections = [];
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var entry in _connections.Values)
            {
                connections.AddRange(entry);
            }

            _connections.Clear();
        }

        foreach (var connection in connections)
        {
            connection.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    public static string GetKey(RuntimeUpstream upstream)
    {
        return $"{upstream.Protocol}|{upstream.Scheme}|{upstream.Address}|{upstream.Port}|sni={upstream.EffectiveSniHost}|validate={upstream.Tls.ValidateCertificate}|alpn=h3|qpack=static-zero";
    }

    private bool TryReserveExistingConnection(
        string key,
        TimeSpan idleLifetime,
        out Http3UpstreamPooledConnection pooled)
    {
        pooled = null!;
        List<Http3UpstreamPooledConnection>? connections;
        lock (_gate)
        {
            if (!_connections.TryGetValue(key, out connections))
            {
                return false;
            }
        }

        foreach (var connection in connections)
        {
            if (!connection.TryReserveStream(idleLifetime))
            {
                continue;
            }

            pooled = connection;
            return true;
        }

        return false;
    }

    private async ValueTask PruneExpiredIdleConnectionsAsync(string key, TimeSpan idleLifetime)
    {
        List<Http3UpstreamPooledConnection> expired = [];
        lock (_gate)
        {
            if (!_connections.TryGetValue(key, out var connections))
            {
                return;
            }

            for (var index = connections.Count - 1; index >= 0; index--)
            {
                var connection = connections[index];
                if (!connection.ShouldPrune(idleLifetime))
                {
                    continue;
                }

                connections.RemoveAt(index);
                expired.Add(connection);
            }

            if (connections.Count == 0)
            {
                _connections.Remove(key);
            }
        }

        foreach (var connection in expired)
        {
            await connection.DisposeAsync();
        }
    }

    private async ValueTask<Http3UpstreamConnection> OpenReservedStreamAsync(
        string key,
        Http3UpstreamPooledConnection pooled,
        RuntimeTimeouts timeouts,
        int maxFramePayloadBytes,
        CancellationToken cancellationToken)
    {
        try
        {
            return await Http3UpstreamConnection.OpenStreamAsync(
                pooled,
                timeouts,
                _metrics,
                maxFramePayloadBytes,
                cancellationToken);
        }
        catch
        {
            RemoveConnection(key, pooled);
            await pooled.DisposeAsync();
            throw;
        }
    }

    private void RemoveConnection(string key, Http3UpstreamPooledConnection pooled)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(key, out var connections))
            {
                return;
            }

            connections.Remove(pooled);
            if (connections.Count == 0)
            {
                _connections.Remove(key);
            }
        }
    }

    private SemaphoreSlim GetKeyGate(string key)
    {
        lock (_gate)
        {
            if (!_keyGates.TryGetValue(key, out var gate))
            {
                gate = new SemaphoreSlim(1, 1);
                _keyGates.Add(key, gate);
            }

            return gate;
        }
    }

    private int ConnectionCount(string key)
    {
        lock (_gate)
        {
            return _connections.TryGetValue(key, out var connections)
                ? connections.Count
                : 0;
        }
    }

    private void AddConnection(string key, Http3UpstreamPooledConnection pooled)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(key, out var connections))
            {
                connections = [];
                _connections.Add(key, connections);
            }

            connections.Add(pooled);
        }
    }

    private static int MaxConnectionsFor(RuntimeConnectionLimits limits)
    {
        return Math.Clamp(limits.MaxIdleUpstreamConnectionsPerUpstream, 1, 64);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(Http3UpstreamConnectionPool));
        }
    }
}
