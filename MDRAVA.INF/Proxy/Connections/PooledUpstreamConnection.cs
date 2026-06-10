using System.Net.Sockets;
using MDRAVA.BLL.Configuration;

namespace MDRAVA.INF.Proxy.Connections;

public sealed class PooledUpstreamConnection : IDisposable
{
    public PooledUpstreamConnection(
        string key,
        RuntimeUpstream upstream,
        Socket socket,
        Stream stream)
    {
        Key = key;
        Upstream = upstream;
        Socket = socket;
        Stream = stream;
        LastUsedUtc = DateTimeOffset.UtcNow;
    }

    public string Key { get; }

    public RuntimeUpstream Upstream { get; }

    public Socket Socket { get; }

    public Stream Stream { get; }

    public DateTimeOffset LastUsedUtc { get; set; }

    public bool CanReturnToPool { get; set; }

    public int MaxIdleConnections { get; set; }

    public void Dispose()
    {
        Stream.Dispose();
        Socket.Dispose();
    }
}
