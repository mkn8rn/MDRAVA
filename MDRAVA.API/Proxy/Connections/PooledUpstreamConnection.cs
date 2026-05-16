using System.Net.Sockets;
using MDRAVA.API.Proxy.Configuration.Runtime;

namespace MDRAVA.API.Proxy.Connections;

public sealed class PooledUpstreamConnection : IDisposable
{
    public PooledUpstreamConnection(
        string key,
        RuntimeUpstream upstream,
        Socket socket)
    {
        Key = key;
        Upstream = upstream;
        Socket = socket;
        Stream = new NetworkStream(socket, ownsSocket: false);
        LastUsedUtc = DateTimeOffset.UtcNow;
    }

    public string Key { get; }

    public RuntimeUpstream Upstream { get; }

    public Socket Socket { get; }

    public NetworkStream Stream { get; }

    public DateTimeOffset LastUsedUtc { get; set; }

    public bool CanReturnToPool { get; set; }

    public int MaxIdleConnections { get; set; }

    public void Dispose()
    {
        Stream.Dispose();
        Socket.Dispose();
    }
}
