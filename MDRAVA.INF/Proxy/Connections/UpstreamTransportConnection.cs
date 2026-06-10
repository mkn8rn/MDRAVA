using System.Net.Sockets;
using MDRAVA.BLL.Configuration;

namespace MDRAVA.INF.Proxy.Connections;

public sealed class UpstreamTransportConnection : IDisposable
{
    public UpstreamTransportConnection(
        RuntimeUpstream upstream,
        Socket socket,
        Stream stream)
    {
        Upstream = upstream;
        Socket = socket;
        Stream = stream;
    }

    public RuntimeUpstream Upstream { get; }

    public Socket Socket { get; }

    public Stream Stream { get; }

    public void Dispose()
    {
        Stream.Dispose();
        Socket.Dispose();
    }
}
