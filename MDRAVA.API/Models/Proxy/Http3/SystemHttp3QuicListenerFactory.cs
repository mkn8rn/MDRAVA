#pragma warning disable CA1416
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Metrics;
using MDRAVA.API.Proxy.Tls;

namespace MDRAVA.API.Proxy.Http3;

public sealed class SystemHttp3QuicListenerFactory : IHttp3QuicListenerFactory
{
    private readonly ProxyMetrics _metrics;
    private readonly ILogger<SystemHttp3QuicListenerFactory> _logger;

    public SystemHttp3QuicListenerFactory(
        ProxyMetrics metrics,
        ILogger<SystemHttp3QuicListenerFactory> logger)
    {
        _metrics = metrics;
        _logger = logger;
    }

    public bool IsSupported => QuicListener.IsSupported && QuicConnection.IsSupported;

    public async ValueTask<QuicListener> ListenAsync(
        RuntimeListener listener,
        ProxyConfigurationSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (!IsSupported)
        {
            throw new InvalidOperationException("quic_runtime_not_supported");
        }

        var listenAddress = IPAddress.Parse(listener.Address);
        var listenEndPoint = new IPEndPoint(listenAddress, listener.Port);
        var options = new QuicListenerOptions
        {
            ListenEndPoint = listenEndPoint,
            ListenBacklog = listener.Backlog,
            ApplicationProtocols = ListenerProtocolAdvertisement.BuildHttp3PreviewAlpn(listener.Protocols),
            ConnectionOptionsCallback = (_, clientHello, _) =>
            {
                var certificate = SelectCertificate(snapshot, listener, clientHello.ServerName);
                if (certificate is null)
                {
                    _metrics.TlsNoCertificateForSni();
                    _logger.LogDebug(
                        "No QUIC certificate matched SNI host {HostName} for listener {ListenerName}.",
                        clientHello.ServerName ?? "<none>",
                        listener.Name);
                    throw new AuthenticationException("no_certificate");
                }

                return ValueTask.FromResult(new QuicServerConnectionOptions
                {
                    ServerAuthenticationOptions = new SslServerAuthenticationOptions
                    {
                        EnabledSslProtocols = SslProtocols.Tls13,
                        ClientCertificateRequired = false,
                        CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                        ApplicationProtocols = ListenerProtocolAdvertisement.BuildHttp3PreviewAlpn(listener.Protocols),
                        ServerCertificate = certificate
                    },
                    MaxInboundBidirectionalStreams = Math.Max(1, listener.Http2Limits.MaxConcurrentStreams),
                    MaxInboundUnidirectionalStreams = 8,
                    IdleTimeout = snapshot.Timeouts.ClientKeepAliveIdleTimeout,
                    HandshakeTimeout = snapshot.Timeouts.TlsHandshakeTimeout,
                    DefaultCloseErrorCode = 0x100,
                    DefaultStreamErrorCode = 0x100
                });
            }
        };

        return await QuicListener.ListenAsync(options, cancellationToken);
    }

    private static X509Certificate2? SelectCertificate(
        ProxyConfigurationSnapshot snapshot,
        RuntimeListener listener,
        string? hostName)
    {
        return TlsCertificateSelector.SelectCertificate(snapshot, listener, hostName);
    }
}
