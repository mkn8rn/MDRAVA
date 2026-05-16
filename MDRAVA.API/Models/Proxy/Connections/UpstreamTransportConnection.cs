using System.Net.Sockets;

namespace MDRAVA.API.Proxy.Connections;

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
