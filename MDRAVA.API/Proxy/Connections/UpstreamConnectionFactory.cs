using System.Net;
using System.Net.Sockets;
using MDRAVA.API.Proxy.Configuration.Runtime;

namespace MDRAVA.API.Proxy.Connections;

public sealed class UpstreamConnectionFactory
{
    public async ValueTask<Socket> ConnectAsync(RuntimeUpstream upstream, CancellationToken cancellationToken)
    {
        var addresses = await ResolveAddressesAsync(upstream, cancellationToken);
        Exception? lastException = null;

        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };

            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, upstream.Port), cancellationToken);
                return socket;
            }
            catch (OperationCanceledException)
            {
                socket.Dispose();
                throw;
            }
            catch (Exception exception) when (exception is SocketException or IOException)
            {
                lastException = exception;
                socket.Dispose();
            }
        }

        throw new IOException(
            $"Unable to connect to upstream '{upstream.Name}' at {upstream.Address}:{upstream.Port}.",
            lastException);
    }

    private static async ValueTask<IPAddress[]> ResolveAddressesAsync(
        RuntimeUpstream upstream,
        CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(upstream.Address, out var address))
        {
            return [address];
        }

        return await Dns.GetHostAddressesAsync(upstream.Address, cancellationToken);
    }
}
