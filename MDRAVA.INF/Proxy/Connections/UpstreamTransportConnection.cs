using System.Net.Sockets;
using MDRAVA.BLL.ControlPlane.Upstreams;

namespace MDRAVA.INF.Proxy.Connections;

public sealed class UpstreamTransportConnection : IDisposable
{
    public UpstreamTransportConnection(
        UpstreamTransportEndpoint endpoint,
        Socket socket,
        Stream stream)
    {
        Endpoint = endpoint;
        Socket = socket;
        Stream = stream;
    }

    public UpstreamTransportEndpoint Endpoint { get; }

    public Socket Socket { get; }

    public Stream Stream { get; }

    public void Dispose()
    {
        Stream.Dispose();
        Socket.Dispose();
    }
}
