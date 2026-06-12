using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Upstreams;
using MDRAVA.BLL.ControlPlane.Timeouts;

namespace MDRAVA.INF.Proxy.Connections;

public sealed class UpstreamConnectionFactory
{
    public async ValueTask<UpstreamTransportConnection> ConnectAsync(
        UpstreamTransportEndpoint endpoint,
        TimeSpan connectTimeout,
        CancellationToken cancellationToken)
    {
        var addresses = await ProxyTimeoutPolicy.RunAsync(
            timeoutToken => ResolveAddressesAsync(endpoint, timeoutToken),
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
                        await socket.ConnectAsync(new IPEndPoint(address, endpoint.Port), timeoutToken);
                    },
                    connectTimeout,
                    ProxyTimeoutKind.UpstreamConnect,
                    cancellationToken);
                var stream = await CreateStreamAsync(socket, endpoint, connectTimeout, cancellationToken);
                return new UpstreamTransportConnection(endpoint, socket, stream);
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
            $"Unable to connect to upstream '{endpoint.Name}' at {endpoint.Address}:{endpoint.Port}.",
            lastException);
    }

    private static async ValueTask<Stream> CreateStreamAsync(
        Socket socket,
        UpstreamTransportEndpoint endpoint,
        TimeSpan connectTimeout,
        CancellationToken cancellationToken)
    {
        var networkStream = new NetworkStream(socket, ownsSocket: false);
        if (!string.Equals(endpoint.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            return networkStream;
        }

        var tlsStream = new SslStream(
            networkStream,
            leaveInnerStreamOpen: false,
            endpoint.ValidateCertificate
                ? null
                : static (_, _, _, _) => true);
        var targetHost = endpoint.EffectiveSniHost;

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
                            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                            ApplicationProtocols = BuildApplicationProtocols(endpoint)
                        },
                        timeoutToken);
                },
                connectTimeout,
                ProxyTimeoutKind.UpstreamConnect,
                cancellationToken);
            if (RuntimeUpstreamProtocol.IsHttp2(endpoint.Protocol)
                && tlsStream.NegotiatedApplicationProtocol != SslApplicationProtocol.Http2)
            {
                throw new UpstreamTlsException(
                    $"TLS ALPN negotiation for upstream '{endpoint.Name}' selected '{FormatNegotiatedProtocol(tlsStream.NegotiatedApplicationProtocol)}' instead of 'h2'.",
                    new AuthenticationException("Upstream did not negotiate HTTP/2."));
            }

            return tlsStream;
        }
        catch (UpstreamTlsException)
        {
            await tlsStream.DisposeAsync();
            throw;
        }
        catch (Exception exception) when (exception is AuthenticationException or IOException)
        {
            await tlsStream.DisposeAsync();
            throw new UpstreamTlsException($"TLS authentication failed for upstream '{endpoint.Name}'.", exception);
        }
    }

    private static async ValueTask<IPAddress[]> ResolveAddressesAsync(
        UpstreamTransportEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(endpoint.Address, out var address))
        {
            return [address];
        }

        return await Dns.GetHostAddressesAsync(endpoint.Address, cancellationToken);
    }

    private static List<SslApplicationProtocol>? BuildApplicationProtocols(UpstreamTransportEndpoint endpoint)
    {
        if (RuntimeUpstreamProtocol.IsHttp3(endpoint.Protocol))
        {
            throw new InvalidOperationException("HTTP/3 upstreams use the QUIC upstream transport, not the TCP upstream connection factory.");
        }

        return RuntimeUpstreamProtocol.IsHttp2(endpoint.Protocol)
            ? [SslApplicationProtocol.Http2]
            : null;
    }

    private static string FormatNegotiatedProtocol(SslApplicationProtocol protocol)
    {
        if (protocol == SslApplicationProtocol.Http2)
        {
            return "h2";
        }

        if (protocol == SslApplicationProtocol.Http11)
        {
            return "http/1.1";
        }

        return protocol.Protocol.Length == 0
            ? "none"
            : Encoding.ASCII.GetString(protocol.Protocol.Span);
    }
}
