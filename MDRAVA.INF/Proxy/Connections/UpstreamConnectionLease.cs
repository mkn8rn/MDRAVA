namespace MDRAVA.INF.Proxy.Connections;

public sealed class UpstreamConnectionLease : IAsyncDisposable
{
    private readonly UpstreamConnectionPool _pool;
    private bool _returned;

    internal UpstreamConnectionLease(
        UpstreamConnectionPool pool,
        PooledUpstreamConnection connection)
    {
        _pool = pool;
        Connection = connection;
    }

    internal PooledUpstreamConnection Connection { get; }

    public Stream Stream => Connection.Stream;

    public void MarkReusable()
    {
        Connection.CanReturnToPool = true;
    }

    public void MarkUnusable()
    {
        Connection.CanReturnToPool = false;
    }

    public ValueTask DisposeAsync()
    {
        if (_returned)
        {
            return ValueTask.CompletedTask;
        }

        _returned = true;
        _pool.Return(Connection);
        return ValueTask.CompletedTask;
    }
}
