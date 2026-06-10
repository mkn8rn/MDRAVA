using System.Net.Sockets;
using MDRAVA.BLL.Configuration;

namespace MDRAVA.INF.Proxy.Connections;

internal sealed class PooledUpstreamConnection : IDisposable
{
    public PooledUpstreamConnection(
        string key,
        RuntimeUpstream upstream,
        Socket socket,
        Stream stream,
        DateTimeOffset lastUsedUtc)
    {
        Key = key;
        Upstream = upstream;
        Socket = socket;
        Stream = stream;
        LastUsedUtc = lastUsedUtc;
    }

    public string Key { get; }

    public RuntimeUpstream Upstream { get; }

    public Socket Socket { get; }

    public Stream Stream { get; }

    public DateTimeOffset LastUsedUtc { get; private set; }

    public bool CanReturnToPool { get; private set; }

    public int MaxIdleConnections { get; private set; }

    public void MarkBorrowed(int maxIdleConnections)
    {
        CanReturnToPool = false;
        MaxIdleConnections = maxIdleConnections;
    }

    public void MarkReusable()
    {
        CanReturnToPool = true;
    }

    public void MarkUnusable()
    {
        CanReturnToPool = false;
    }

    public void MarkReturnedIdle(DateTimeOffset returnedAtUtc)
    {
        LastUsedUtc = returnedAtUtc;
        CanReturnToPool = false;
    }

    public void Dispose()
    {
        Stream.Dispose();
        Socket.Dispose();
    }
}
