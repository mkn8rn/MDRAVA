using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Forwarding;

namespace MDRAVA.API.Proxy.Connections;

public sealed class UpstreamConnectionFactory
{
    public async ValueTask<UpstreamTransportConnection> ConnectAsync(
        RuntimeUpstream upstream,
        TimeSpan connectTimeout,
        CancellationToken cancellationToken)
    {
        var addresses = await ProxyTimeoutPolicy.RunAsync(
            timeoutToken => ResolveAddressesAsync(upstream, timeoutToken),
            connectTimeout,
            ProxyTimeoutKind.UpstreamConnect,
            cancellationToken);
        Exception? lastException = null;

        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };

            try
            {
                await ProxyTimeoutPolicy.RunAsync(
                    async timeoutToken =>
                    {
                        await socket.ConnectAsync(new IPEndPoint(address, upstream.Port), timeoutToken);
                    },
                    connectTimeout,
                    ProxyTimeoutKind.UpstreamConnect,
                    cancellationToken);
                var stream = await CreateStreamAsync(socket, upstream, connectTimeout, cancellationToken);
                return new UpstreamTransportConnection(upstream, socket, stream);
            }
            catch (OperationCanceledException)
            {
                socket.Dispose();
                throw;
            }
            catch (Exception exception) when (exception is SocketException or IOException or AuthenticationException)
            {
                lastException = exception;
                socket.Dispose();
            }
        }

        if (lastException is UpstreamTlsException tlsException)
        {
            throw tlsException;
        }

        throw new IOException(
            $"Unable to connect to upstream '{upstream.Name}' at {upstream.Address}:{upstream.Port}.",
            lastException);
    }

    private static async ValueTask<Stream> CreateStreamAsync(
        Socket socket,
        RuntimeUpstream upstream,
        TimeSpan connectTimeout,
        CancellationToken cancellationToken)
    {
        var networkStream = new NetworkStream(socket, ownsSocket: false);
        if (!string.Equals(upstream.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            return networkStream;
        }

        var tlsStream = new SslStream(
            networkStream,
            leaveInnerStreamOpen: false,
            upstream.Tls.ValidateCertificate
                ? null
                : static (_, _, _, _) => true);
        var targetHost = upstream.EffectiveSniHost;

        try
        {
            await ProxyTimeoutPolicy.RunAsync(
                async timeoutToken =>
                {
                    await tlsStream.AuthenticateAsClientAsync(
                        new SslClientAuthenticationOptions
                        {
                            TargetHost = targetHost,
                            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                            CertificateRevocationCheckMode = X509RevocationMode.NoCheck
                        },
                        timeoutToken);
                },
                connectTimeout,
                ProxyTimeoutKind.UpstreamConnect,
                cancellationToken);
            return tlsStream;
        }
        catch (Exception exception) when (exception is AuthenticationException or IOException)
        {
            await tlsStream.DisposeAsync();
            throw new UpstreamTlsException($"TLS authentication failed for upstream '{upstream.Name}'.", exception);
        }
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
