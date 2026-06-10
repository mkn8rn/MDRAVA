using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane;
using MDRAVA.BLL.ControlPlane.Metrics;
using MDRAVA.BLL.ControlPlane.RuntimeGuards;
using MDRAVA.BLL.ControlPlane.Timeouts;
using Microsoft.Extensions.Logging;

namespace MDRAVA.INF.Proxy.Tls;

public sealed class TlsConnectionAuthenticator
{
    private readonly ProxyMetrics _metrics;
    private readonly ProxyAdmissionController _admission;
    private readonly ILogger<TlsConnectionAuthenticator> _logger;

    public TlsConnectionAuthenticator(
        ProxyMetrics metrics,
        ProxyAdmissionController admission,
        ILogger<TlsConnectionAuthenticator> logger)
    {
        _metrics = metrics;
        _admission = admission;
        _logger = logger;
    }

    public async ValueTask<TlsAuthenticationResult?> AuthenticateAsync(
        Stream transportStream,
        ProxyConfigurationSnapshot snapshot,
        RuntimeListener listener,
        CancellationToken cancellationToken)
    {
        _metrics.TlsHandshakeAttempted();
        using var handshakeLease = _admission.TryAcquireTlsHandshake(snapshot.Limits.MaxConcurrentTlsHandshakes);
        if (handshakeLease is null)
        {
            _logger.LogDebug("Rejected TLS handshake for listener {ListenerName} because the concurrent handshake limit is exhausted.", listener.Name);
            return null;
        }

        var sslStream = new SslStream(transportStream, false);
        var options = new SslServerAuthenticationOptions
        {
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            ClientCertificateRequired = false,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
            ApplicationProtocols = ListenerProtocolAdvertisement.BuildTcpAlpn(listener.Protocols),
            ServerCertificateSelectionCallback = (_, hostName) =>
                SelectCertificateForHandshake(snapshot, listener, hostName) ?? null!
        };

        try
        {
            await ProxyTimeoutPolicy.RunAsync(
                async timeoutToken =>
                {
                    await sslStream.AuthenticateAsServerAsync(options, timeoutToken);
                },
                snapshot.Timeouts.TlsHandshakeTimeout,
                ProxyTimeoutKind.TlsHandshake,
                cancellationToken);
            _metrics.TlsHandshakeSucceeded();
            return new TlsAuthenticationResult(sslStream, sslStream.NegotiatedApplicationProtocol);
        }
        catch (ProxyTimeoutException)
        {
            _metrics.TlsHandshakeTimedOut();
            _logger.LogDebug("TLS handshake timed out for listener {ListenerName}.", listener.Name);
            await sslStream.DisposeAsync();
            return null;
        }
        catch (AuthenticationException exception)
        {
            _metrics.TlsHandshakeFailed();
            _logger.LogDebug(exception, "TLS handshake failed for listener {ListenerName}.", listener.Name);
            await sslStream.DisposeAsync();
            return null;
        }
        catch (IOException exception)
        {
            _metrics.TlsHandshakeFailed();
            _logger.LogDebug(exception, "TLS handshake ended with I/O failure for listener {ListenerName}.", listener.Name);
            await sslStream.DisposeAsync();
            return null;
        }
        catch (Exception exception)
        {
            _metrics.TlsHandshakeFailed();
            _logger.LogError(exception, "TLS handshake failed unexpectedly for listener {ListenerName}.", listener.Name);
            await sslStream.DisposeAsync();
            return null;
        }
    }

    private System.Security.Cryptography.X509Certificates.X509Certificate2? SelectCertificateForHandshake(
        ProxyConfigurationSnapshot snapshot,
        RuntimeListener listener,
        string? hostName)
    {
        var certificate = TlsCertificateSelector.SelectCertificate(
            new TlsCertificateSelectionInput(
                snapshot.Certificates,
                listener.DefaultCertificateId,
                listener.SniCertificates,
                hostName));
        if (certificate is null)
        {
            RecordNoCertificate(listener, hostName);
        }

        return certificate;
    }

    private void RecordNoCertificate(RuntimeListener listener, string? hostName)
    {
        _metrics.TlsNoCertificateForSni();
        _logger.LogDebug(
            "No TLS certificate matched SNI host {HostName} for listener {ListenerName}.",
            hostName ?? "<none>",
            listener.Name);
    }
}
