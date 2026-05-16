using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Forwarding;
using MDRAVA.API.Proxy.Metrics;

namespace MDRAVA.API.Proxy.Tls;

public sealed class TlsConnectionAuthenticator
{
    private readonly ProxyMetrics _metrics;
    private readonly ILogger<TlsConnectionAuthenticator> _logger;

    public TlsConnectionAuthenticator(
        ProxyMetrics metrics,
        ILogger<TlsConnectionAuthenticator> logger)
    {
        _metrics = metrics;
        _logger = logger;
    }

    public async ValueTask<SslStream?> AuthenticateAsync(
        Stream transportStream,
        ProxyConfigurationSnapshot snapshot,
        RuntimeListener listener,
        CancellationToken cancellationToken)
    {
        _metrics.TlsHandshakeAttempted();
        var sslStream = new SslStream(transportStream, false);
        var options = new SslServerAuthenticationOptions
        {
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            ClientCertificateRequired = false,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
            ServerCertificateSelectionCallback = (_, hostName) =>
                SelectCertificate(snapshot, listener, hostName) ?? null!
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
            return sslStream;
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

    private X509Certificate2? SelectCertificate(
        ProxyConfigurationSnapshot snapshot,
        RuntimeListener listener,
        string? hostName)
    {
        if (!string.IsNullOrWhiteSpace(hostName))
        {
            foreach (var binding in listener.SniCertificates)
            {
                if (string.Equals(binding.HostName, hostName, StringComparison.OrdinalIgnoreCase)
                    && snapshot.Certificates.TryGetValue(binding.CertificateId, out var certificate))
                {
                    return certificate.Certificate;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(listener.DefaultCertificateId)
            && snapshot.Certificates.TryGetValue(listener.DefaultCertificateId, out var defaultCertificate))
        {
            return defaultCertificate.Certificate;
        }

        _metrics.TlsNoCertificateForSni();
        _logger.LogDebug(
            "No TLS certificate matched SNI host {HostName} for listener {ListenerName}.",
            hostName ?? "<none>",
            listener.Name);
        return null;
    }
}
