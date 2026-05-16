using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using MDRAVA.API.Proxy.Metrics;
using MDRAVA.API.Proxy.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MDRAVA.Tests;

internal static class ProxyIntegrationTests
{
    public static async Task ProxiesSingleGetToUpstream()
    {
        var proxyPort = GetFreeTcpPort();
        var upstreamPort = GetFreeTcpPort();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var dataDirectory = Path.Combine(Path.GetTempPath(), $"mdrava-integration-{Guid.NewGuid():N}");

        ConfigurationTests.WriteSite(dataDirectory, "smoke.json", proxyPort, upstreamPort);
        var upstreamTask = RunSingleResponseUpstreamAsync(upstreamPort, timeout.Token);

        try
        {
            using var host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration(builder =>
                {
                    builder.Sources.Clear();
                    builder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Mdrava:DataDirectory"] = dataDirectory
                    });
                })
                .ConfigureLogging(logging => logging.ClearProviders())
                .ConfigureServices((context, services) =>
                {
                    services.AddProxyDataPlane(context.Configuration);
                })
                .Build();

            await host.StartAsync(timeout.Token);

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, proxyPort, timeout.Token);

                await using var stream = client.GetStream();
                var requestBytes = Encoding.ASCII.GetBytes("GET /smoke HTTP/1.1\r\nHost: smoke.test\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(requestBytes, timeout.Token);

                var responseText = await ReadToEndAsync(stream, timeout.Token);

                AssertEx.True(responseText.Contains("200 OK", StringComparison.Ordinal), responseText);
                AssertEx.True(responseText.EndsWith("proxied", StringComparison.Ordinal), responseText);

                var upstreamRequest = await upstreamTask.WaitAsync(timeout.Token);
                AssertEx.True(upstreamRequest.StartsWith("GET /smoke HTTP/1.1", StringComparison.Ordinal), upstreamRequest);
                AssertEx.True(upstreamRequest.Contains("Connection: keep-alive", StringComparison.OrdinalIgnoreCase), upstreamRequest);
            }
            finally
            {
                await host.StopAsync(CancellationToken.None);
            }
        }
        finally
        {
            try
            {
                if (Directory.Exists(dataDirectory))
                {
                    Directory.Delete(dataDirectory, recursive: true);
                }
            }
            catch
            {
            }
        }
    }

    public static async Task ProxiesFixedLengthRequestAndResponse()
    {
        var request = "POST /submit HTTP/1.1\r\nHost: fixed.test\r\nContent-Length: 11\r\n\r\nhello world";
        var upstreamResponse = "HTTP/1.1 201 Created\r\nContent-Length: 7\r\nContent-Type: text/plain\r\n\r\ncreated";

        var result = await RunProxyScenarioAsync(request, upstreamResponse);

        AssertEx.True(result.ClientResponse.Contains("201 Created", StringComparison.Ordinal), result.ClientResponse);
        AssertEx.True(result.ClientResponse.EndsWith("created", StringComparison.Ordinal), result.ClientResponse);
        AssertEx.True(result.UpstreamRequest.Contains("Content-Length: 11", StringComparison.OrdinalIgnoreCase), result.UpstreamRequest);
        AssertEx.True(result.UpstreamRequest.EndsWith("hello world", StringComparison.Ordinal), result.UpstreamRequest);
    }

    public static async Task ProxiesChunkedRequestAndResponse()
    {
        var request = "POST /chunks HTTP/1.1\r\nHost: chunk.test\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nhello\r\n6\r\n world\r\n0\r\nX-Trailer: ok\r\n\r\n";
        var upstreamResponse = "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nhello\r\n6\r\n world\r\n0\r\n\r\n";

        var result = await RunProxyScenarioAsync(request, upstreamResponse);

        AssertEx.True(result.ClientResponse.Contains("Transfer-Encoding: chunked", StringComparison.OrdinalIgnoreCase), result.ClientResponse);
        AssertEx.True(result.ClientResponse.EndsWith("0\r\n\r\n", StringComparison.Ordinal), result.ClientResponse);
        AssertEx.True(result.UpstreamRequest.Contains("Transfer-Encoding: chunked", StringComparison.OrdinalIgnoreCase), result.UpstreamRequest);
        AssertEx.True(result.UpstreamRequest.Contains("X-Trailer: ok", StringComparison.Ordinal), result.UpstreamRequest);
    }

    public static async Task DoesNotRelayHeadResponseBody()
    {
        var result = await RunProxyScenarioAsync(
            "HEAD /head HTTP/1.1\r\nHost: head.test\r\n\r\n",
            "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello",
            readBodyFromUpstreamRequest: false);

        AssertEx.True(result.ClientResponse.Contains("200 OK", StringComparison.Ordinal), result.ClientResponse);
        AssertEx.False(result.ClientResponse.EndsWith("hello", StringComparison.Ordinal), result.ClientResponse);
    }

    public static async Task ProxiesNoContentWithoutBody()
    {
        var result = await RunProxyScenarioAsync(
            "GET /empty HTTP/1.1\r\nHost: empty.test\r\n\r\n",
            "HTTP/1.1 204 No Content\r\nContent-Length: 5\r\n\r\nhello",
            readBodyFromUpstreamRequest: false);

        AssertEx.True(result.ClientResponse.Contains("204 No Content", StringComparison.Ordinal), result.ClientResponse);
        AssertEx.False(result.ClientResponse.EndsWith("hello", StringComparison.Ordinal), result.ClientResponse);
    }

    public static async Task ProxiesNotModifiedWithoutBody()
    {
        var result = await RunProxyScenarioAsync(
            "GET /cached HTTP/1.1\r\nHost: cache.test\r\n\r\n",
            "HTTP/1.1 304 Not Modified\r\nContent-Length: 5\r\n\r\nhello",
            readBodyFromUpstreamRequest: false);

        AssertEx.True(result.ClientResponse.Contains("304 Not Modified", StringComparison.Ordinal), result.ClientResponse);
        AssertEx.False(result.ClientResponse.EndsWith("hello", StringComparison.Ordinal), result.ClientResponse);
    }

    public static async Task RejectsInvalidRequestFraming()
    {
        var result = await RunProxyScenarioAsync(
            "POST /bad HTTP/1.1\r\nHost: bad.test\r\nContent-Length: 1\r\nTransfer-Encoding: chunked\r\n\r\n0\r\n\r\n",
            "HTTP/1.1 500 Should Not Happen\r\nContent-Length: 0\r\n\r\n",
            expectUpstreamConnection: false);

        AssertEx.True(result.ClientResponse.Contains("400 Bad Request", StringComparison.Ordinal), result.ClientResponse);
        AssertEx.Equal("", result.UpstreamRequest);
    }

    public static async Task RejectsMalformedChunkedRequestBody()
    {
        var result = await RunProxyScenarioAsync(
            "POST /bad-chunk HTTP/1.1\r\nHost: bad.test\r\nTransfer-Encoding: chunked\r\n\r\nZ\r\nbad\r\n0\r\n\r\n",
            "HTTP/1.1 500 Should Not Happen\r\nContent-Length: 0\r\n\r\n",
            readBodyFromUpstreamRequest: false,
            expectUpstreamConnection: false);

        AssertEx.True(result.ClientResponse.Contains("400 Bad Request", StringComparison.Ordinal), result.ClientResponse);
    }

    public static async Task FiltersHopByHopRequestHeaders()
    {
        var result = await RunProxyScenarioAsync(
            "GET /headers HTTP/1.1\r\nHost: header.test\r\nConnection: x-private, close\r\nX-Private: secret\r\nKeep-Alive: timeout=5\r\n\r\n",
            "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok",
            readBodyFromUpstreamRequest: false);

        AssertEx.False(result.UpstreamRequest.Contains("X-Private", StringComparison.OrdinalIgnoreCase), result.UpstreamRequest);
        AssertEx.False(result.UpstreamRequest.Contains("\r\nKeep-Alive:", StringComparison.OrdinalIgnoreCase), result.UpstreamRequest);
    }

    public static async Task PreservesHostHeader()
    {
        var result = await RunProxyScenarioAsync(
            "GET /host HTTP/1.1\r\nHost: original.test\r\n\r\n",
            "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok",
            readBodyFromUpstreamRequest: false);

        AssertEx.True(result.UpstreamRequest.Contains("Host: original.test", StringComparison.OrdinalIgnoreCase), result.UpstreamRequest);
    }

    public static async Task TimesOutIncompleteRequestHead()
    {
        var result = await RunProxyScenarioAsync(
            "GET /slow",
            "",
            expectUpstreamConnection: false,
            timeoutMs: 150);

        AssertEx.True(result.ClientResponse.Contains("408 Request Timeout", StringComparison.Ordinal), result.ClientResponse);
        AssertEx.Equal(1L, result.Metrics.ClientRequestHeadTimeouts);
    }

    public static async Task TimesOutIncompleteContentLengthRequestBody()
    {
        var result = await RunProxyScenarioAsync(
            "POST /slow-body HTTP/1.1\r\nHost: body.test\r\nContent-Length: 10\r\n\r\nabc",
            "HTTP/1.1 500 Should Not Happen\r\nContent-Length: 0\r\n\r\n",
            readBodyFromUpstreamRequest: false,
            timeoutMs: 150);

        AssertEx.True(result.ClientResponse.Contains("408 Request Timeout", StringComparison.Ordinal), result.ClientResponse);
        AssertEx.Equal(1L, result.Metrics.ClientRequestBodyTimeouts);
    }

    public static async Task TimesOutIncompleteChunkedRequestBody()
    {
        var result = await RunProxyScenarioAsync(
            "POST /slow-chunk HTTP/1.1\r\nHost: chunk.test\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nabc",
            "HTTP/1.1 500 Should Not Happen\r\nContent-Length: 0\r\n\r\n",
            readBodyFromUpstreamRequest: false,
            timeoutMs: 150);

        AssertEx.True(result.ClientResponse.Contains("408 Request Timeout", StringComparison.Ordinal), result.ClientResponse);
        AssertEx.Equal(1L, result.Metrics.ClientRequestBodyTimeouts);
    }

    public static async Task UnavailableUpstreamProducesBadGateway()
    {
        var result = await RunProxyScenarioAsync(
            "GET /unavailable HTTP/1.1\r\nHost: upstream.test\r\n\r\n",
            "",
            expectUpstreamConnection: false);

        AssertEx.True(result.ClientResponse.Contains("502 Bad Gateway", StringComparison.Ordinal), result.ClientResponse);
        AssertEx.Equal(1L, result.Metrics.ProxyGenerated502Responses);
    }

    public static async Task UpstreamResponseHeadTimeoutProducesGatewayTimeout()
    {
        var result = await RunProxyScenarioAsync(
            "GET /slow-upstream HTTP/1.1\r\nHost: upstream.test\r\n\r\n",
            "",
            readBodyFromUpstreamRequest: false,
            timeoutMs: 150,
            sendUpstreamResponse: false);

        AssertEx.True(result.ClientResponse.Contains("504 Gateway Timeout", StringComparison.Ordinal), result.ClientResponse);
        AssertEx.Equal(1L, result.Metrics.UpstreamResponseHeadTimeouts);
        AssertEx.Equal(1L, result.Metrics.ProxyGenerated504Responses);
    }

    public static async Task UpstreamContentLengthEarlyCloseClosesAfterStartedResponse()
    {
        var result = await RunProxyScenarioAsync(
            "GET /short HTTP/1.1\r\nHost: upstream.test\r\n\r\n",
            "HTTP/1.1 200 OK\r\nContent-Length: 10\r\n\r\nhello",
            readBodyFromUpstreamRequest: false,
            timeoutMs: 150);

        AssertEx.True(result.ClientResponse.Contains("200 OK", StringComparison.Ordinal), result.ClientResponse);
        AssertEx.False(result.ClientResponse.Contains("502 Bad Gateway", StringComparison.Ordinal), result.ClientResponse);
        AssertEx.False(result.ClientResponse.Contains("504 Gateway Timeout", StringComparison.Ordinal), result.ClientResponse);
        AssertEx.Equal(1L, result.Metrics.UpstreamBodyRelayFailures);
    }

    public static async Task HttpsListenerProxiesGetToUpstream()
    {
        var result = await RunTlsProxyScenarioAsync("home.test");

        AssertEx.True(result.ClientResponse.Contains("200 OK", StringComparison.Ordinal), result.ClientResponse);
        AssertEx.True(result.ClientResponse.EndsWith("proxied", StringComparison.Ordinal), result.ClientResponse);
        AssertEx.True(result.UpstreamRequest.StartsWith("GET /secure HTTP/1.1", StringComparison.Ordinal), result.UpstreamRequest);
        AssertEx.Equal(1L, result.Metrics.TlsHandshakeAttempts);
        AssertEx.Equal(1L, result.Metrics.TlsHandshakeSuccesses);
    }

    public static async Task HttpsListenerSelectsCertificateBySni()
    {
        var result = await RunTlsProxyScenarioAsync("alt.test", configureAltSni: true);

        AssertEx.True(result.RemoteCertificateSubject.Contains("CN=alt.test", StringComparison.Ordinal), result.RemoteCertificateSubject);
        AssertEx.Equal(1L, result.Metrics.TlsHandshakeSuccesses);
    }

    public static async Task HttpsListenerUsesDefaultCertificateForUnmatchedSni()
    {
        var result = await RunTlsProxyScenarioAsync("unmatched.test", configureAltSni: true);

        AssertEx.True(result.RemoteCertificateSubject.Contains("CN=home.test", StringComparison.Ordinal), result.RemoteCertificateSubject);
        AssertEx.Equal(1L, result.Metrics.TlsHandshakeSuccesses);
    }

    public static async Task HttpsListenerUsesDefaultCertificateWithoutSni()
    {
        var result = await RunTlsProxyScenarioAsync("", configureAltSni: true);

        AssertEx.True(result.RemoteCertificateSubject.Contains("CN=home.test", StringComparison.Ordinal), result.RemoteCertificateSubject);
        AssertEx.Equal(1L, result.Metrics.TlsHandshakeSuccesses);
    }

    public static async Task HttpsListenerFailsHandshakeWhenNoCertificateMatches()
    {
        var proxyPort = GetFreeTcpPort();
        var upstreamPort = GetFreeTcpPort();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var dataDirectory = Path.Combine(Path.GetTempPath(), $"mdrava-tls-{Guid.NewGuid():N}");

        try
        {
            TestCertificates.WriteSelfSignedPfx(Path.Combine(dataDirectory, "certs", "home.pfx"), "home.test");
            ConfigurationTests.WriteHttpsSite(
                dataDirectory,
                "tls.json",
                proxyPort,
                upstreamPort,
                "home-cert",
                includeDefault: false);
            ConfigurationTests.WriteOperationalConfig(
                dataDirectory,
                certificateId: "home-cert",
                certificatePath: "certs/home.pfx");

            using var host = BuildProxyHost(dataDirectory);
            await host.StartAsync(timeout.Token);

            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, proxyPort, timeout.Token);
            await using var tlsStream = new SslStream(client.GetStream(), false, (_, _, _, _) => true);

            try
            {
                await tlsStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = "unmatched.test"
                });
                throw new InvalidOperationException("Expected TLS authentication to fail.");
            }
            catch (Exception exception) when (exception is AuthenticationException or IOException)
            {
            }

            var metrics = await WaitForMetricsAsync(
                host.Services.GetRequiredService<ProxyMetrics>(),
                snapshot => snapshot.TlsNoCertificateForSniFailures == 1,
                timeout.Token);

            AssertEx.Equal(1L, metrics.TlsHandshakeAttempts);
            AssertEx.Equal(1L, metrics.TlsNoCertificateForSniFailures);
            AssertEx.Equal(1L, metrics.TlsHandshakeFailures);
            await host.StopAsync(CancellationToken.None);
        }
        finally
        {
            DeleteDirectory(dataDirectory);
        }
    }

    public static async Task HttpsListenerTimesOutIncompleteTlsHandshake()
    {
        var proxyPort = GetFreeTcpPort();
        var upstreamPort = GetFreeTcpPort();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var dataDirectory = Path.Combine(Path.GetTempPath(), $"mdrava-tls-{Guid.NewGuid():N}");

        try
        {
            TestCertificates.WriteSelfSignedPfx(Path.Combine(dataDirectory, "certs", "home.pfx"), "home.test");
            ConfigurationTests.WriteHttpsSite(dataDirectory, "tls.json", proxyPort, upstreamPort, "home-cert");
            ConfigurationTests.WriteOperationalConfig(
                dataDirectory,
                tlsHandshakeTimeoutMs: 150,
                certificateId: "home-cert",
                certificatePath: "certs/home.pfx");

            using var host = BuildProxyHost(dataDirectory);
            await host.StartAsync(timeout.Token);

            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, proxyPort, timeout.Token);

            var metrics = await WaitForMetricsAsync(
                host.Services.GetRequiredService<ProxyMetrics>(),
                snapshot => snapshot.TlsHandshakeTimeouts == 1,
                timeout.Token);

            AssertEx.Equal(1L, metrics.TlsHandshakeAttempts);
            AssertEx.Equal(1L, metrics.TlsHandshakeTimeouts);
            await host.StopAsync(CancellationToken.None);
        }
        finally
        {
            DeleteDirectory(dataDirectory);
        }
    }

    public static async Task PersistentClientProcessesTwoSequentialGetsAndReusesUpstream()
    {
        var result = await RunPersistentClientScenarioAsync(
            [
                "GET /one HTTP/1.1\r\nHost: keep.test\r\n\r\n",
                "GET /two HTTP/1.1\r\nHost: keep.test\r\nConnection: close\r\n\r\n"
            ],
            [
                "HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\none",
                "HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\ntwo"
            ]);

        AssertEx.True(result.ClientResponses[0].EndsWith("one", StringComparison.Ordinal), result.ClientResponses[0]);
        AssertEx.True(result.ClientResponses[1].EndsWith("two", StringComparison.Ordinal), result.ClientResponses[1]);
        AssertEx.Equal(1, result.UpstreamAcceptedConnections);
        AssertEx.Equal(1L, result.Metrics.UpstreamConnectionsOpened);
        AssertEx.Equal(1L, result.Metrics.UpstreamConnectionsReused);
    }

    public static async Task ClientConnectionCloseHeaderClosesAfterResponse()
    {
        var result = await RunPersistentClientScenarioAsync(
            ["GET /close HTTP/1.1\r\nHost: close.test\r\nConnection: close\r\n\r\n"],
            ["HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok"],
            expectClientCloseAfterLastResponse: true);

        AssertEx.True(result.ClientClosedAfterLastResponse);
    }

    public static async Task Http10ClientClosesByDefault()
    {
        var result = await RunPersistentClientScenarioAsync(
            ["GET /old HTTP/1.0\r\nHost: old.test\r\n\r\n"],
            ["HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok"],
            expectClientCloseAfterLastResponse: true);

        AssertEx.True(result.ClientClosedAfterLastResponse);
    }

    public static async Task MaxRequestsPerClientConnectionIsEnforced()
    {
        var result = await RunPersistentClientScenarioAsync(
            ["GET /max HTTP/1.1\r\nHost: max.test\r\n\r\n"],
            ["HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok"],
            maxRequestsPerClientConnection: 1,
            expectClientCloseAfterLastResponse: true);

        AssertEx.True(result.ClientClosedAfterLastResponse);
        AssertEx.Equal(1L, result.Metrics.ClientConnectionsClosedByMaxRequests);
    }

    public static async Task ClientKeepAliveIdleTimeoutClosesConnection()
    {
        var result = await RunPersistentClientScenarioAsync(
            ["GET /idle HTTP/1.1\r\nHost: idle.test\r\n\r\n"],
            ["HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok"],
            clientKeepAliveIdleTimeoutMs: 150,
            expectClientCloseAfterLastResponse: true);

        AssertEx.True(result.ClientClosedAfterLastResponse);
        AssertEx.Equal(1L, result.Metrics.ClientConnectionsClosedByIdleTimeout);
    }

    public static async Task MalformedSecondRequestClosesConnection()
    {
        var result = await RunPersistentClientScenarioAsync(
            [
                "GET /first HTTP/1.1\r\nHost: malformed.test\r\n\r\n",
                "BAD\r\n\r\n"
            ],
            ["HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nfirst"],
            readSecondAsRawClose: true);

        AssertEx.True(result.ClientResponses[0].EndsWith("first", StringComparison.Ordinal), result.ClientResponses[0]);
        AssertEx.True(result.ClientResponses[1].Contains("400 Bad Request", StringComparison.Ordinal), result.ClientResponses[1]);
    }

    public static async Task PersistentClientProxiesContentLengthPost()
    {
        var result = await RunPersistentClientScenarioAsync(
            [
                "POST /post HTTP/1.1\r\nHost: post.test\r\nContent-Length: 5\r\n\r\nhello",
                "GET /done HTTP/1.1\r\nHost: post.test\r\nConnection: close\r\n\r\n"
            ],
            [
                "HTTP/1.1 200 OK\r\nContent-Length: 4\r\n\r\npost",
                "HTTP/1.1 200 OK\r\nContent-Length: 4\r\n\r\ndone"
            ]);

        AssertEx.True(result.UpstreamRequests[0].EndsWith("hello", StringComparison.Ordinal), result.UpstreamRequests[0]);
        AssertEx.Equal(1, result.UpstreamAcceptedConnections);
    }

    public static async Task PersistentClientProxiesChunkedPost()
    {
        var result = await RunPersistentClientScenarioAsync(
            [
                "POST /chunk HTTP/1.1\r\nHost: chunk.test\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nhello\r\n0\r\n\r\n",
                "GET /done HTTP/1.1\r\nHost: chunk.test\r\nConnection: close\r\n\r\n"
            ],
            [
                "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nchunk",
                "HTTP/1.1 200 OK\r\nContent-Length: 4\r\n\r\ndone"
            ]);

        AssertEx.True(result.UpstreamRequests[0].Contains("Transfer-Encoding: chunked", StringComparison.OrdinalIgnoreCase), result.UpstreamRequests[0]);
        AssertEx.Equal(1, result.UpstreamAcceptedConnections);
    }

    public static async Task UpstreamConnectionIsNotReusedAfterResponseConnectionClose()
    {
        var result = await RunPersistentClientScenarioAsync(
            [
                "GET /first HTTP/1.1\r\nHost: upstream-close.test\r\n\r\n",
                "GET /second HTTP/1.1\r\nHost: upstream-close.test\r\nConnection: close\r\n\r\n"
            ],
            [
                "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 5\r\n\r\nfirst",
                "HTTP/1.1 200 OK\r\nContent-Length: 6\r\n\r\nsecond"
            ]);

        AssertEx.Equal(2, result.UpstreamAcceptedConnections);
        AssertEx.Equal(2L, result.Metrics.UpstreamConnectionsOpened);
    }

    public static async Task UpstreamConnectionIsNotReusedAfterPrematureDisconnect()
    {
        var result = await RunPersistentClientScenarioAsync(
            [
                "GET /short HTTP/1.1\r\nHost: short.test\r\nConnection: close\r\n\r\n",
                "GET /next HTTP/1.1\r\nHost: short.test\r\nConnection: close\r\n\r\n"
            ],
            [
                "HTTP/1.1 200 OK\r\nContent-Length: 10\r\n\r\nshort",
                "HTTP/1.1 200 OK\r\nContent-Length: 4\r\n\r\nnext"
            ],
            closeUpstreamAfterEachResponse: true,
            useSeparateClients: true);

        AssertEx.Equal(2, result.UpstreamAcceptedConnections);
        AssertEx.True(result.Metrics.UpstreamConnectionsDiscarded >= 1);
    }

    public static async Task UpstreamConnectionIsNotReusedAfterFramingError()
    {
        var result = await RunPersistentClientScenarioAsync(
            [
                "GET /bad-upstream HTTP/1.1\r\nHost: bad-upstream.test\r\nConnection: close\r\n\r\n",
                "GET /next HTTP/1.1\r\nHost: bad-upstream.test\r\nConnection: close\r\n\r\n"
            ],
            [
                "NOT HTTP\r\n\r\n",
                "HTTP/1.1 200 OK\r\nContent-Length: 4\r\n\r\nnext"
            ],
            closeUpstreamAfterEachResponse: true,
            useSeparateClients: true);

        AssertEx.Equal(2, result.UpstreamAcceptedConnections);
        AssertEx.True(result.Metrics.UpstreamConnectionsDiscarded >= 1);
    }

    private static async Task<string> RunSingleResponseUpstreamAsync(
        int upstreamPort,
        CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Loopback, upstreamPort);
        listener.Start();

        try
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken);
            await using var stream = client.GetStream();

            var requestText = await ReadRequestHeadAsync(stream, cancellationToken);
            var responseBytes = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 7\r\nContent-Type: text/plain\r\n\r\nproxied");

            await stream.WriteAsync(responseBytes, cancellationToken);
            return requestText;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task<ProxyScenarioResult> RunProxyScenarioAsync(
        string clientRequest,
        string upstreamResponse,
        bool readBodyFromUpstreamRequest = true,
        bool expectUpstreamConnection = true,
        int? timeoutMs = null,
        bool sendUpstreamResponse = true)
    {
        var proxyPort = GetFreeTcpPort();
        var upstreamPort = GetFreeTcpPort();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var dataDirectory = Path.Combine(Path.GetTempPath(), $"mdrava-integration-{Guid.NewGuid():N}");

        ConfigurationTests.WriteSite(dataDirectory, "scenario.json", proxyPort, upstreamPort);
        if (timeoutMs.HasValue)
        {
            ConfigurationTests.WriteOperationalConfig(
                dataDirectory,
                clientRequestHeadTimeoutMs: timeoutMs.Value,
                clientRequestBodyIdleTimeoutMs: timeoutMs.Value,
                upstreamConnectTimeoutMs: timeoutMs.Value,
                upstreamResponseHeadTimeoutMs: timeoutMs.Value,
                upstreamResponseBodyIdleTimeoutMs: timeoutMs.Value,
                downstreamWriteTimeoutMs: 1000);
        }
        var upstreamTask = expectUpstreamConnection
            ? RunScenarioUpstreamAsync(upstreamPort, upstreamResponse, readBodyFromUpstreamRequest, sendUpstreamResponse, timeout.Token)
            : Task.FromResult("");

        try
        {
            using var host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration(builder =>
                {
                    builder.Sources.Clear();
                    builder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Mdrava:DataDirectory"] = dataDirectory
                    });
                })
                .ConfigureLogging(logging => logging.ClearProviders())
                .ConfigureServices((context, services) =>
                {
                    services.AddProxyDataPlane(context.Configuration);
                })
                .Build();

            await host.StartAsync(timeout.Token);

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, proxyPort, timeout.Token);
                await using var stream = client.GetStream();
                var requestBytes = Encoding.ASCII.GetBytes(WithConnectionClose(clientRequest));
                await stream.WriteAsync(requestBytes, timeout.Token);

                var clientResponse = await ReadToEndAsync(stream, timeout.Token);
                var upstreamRequest = await upstreamTask.WaitAsync(timeout.Token);
                var metrics = host.Services.GetRequiredService<ProxyMetrics>().Snapshot();
                return new ProxyScenarioResult(clientResponse, upstreamRequest, metrics);
            }
            finally
            {
                await host.StopAsync(CancellationToken.None);
            }
        }
        finally
        {
            try
            {
                if (Directory.Exists(dataDirectory))
                {
                    Directory.Delete(dataDirectory, recursive: true);
                }
            }
            catch
            {
            }
        }
    }

    private static async Task<TlsProxyScenarioResult> RunTlsProxyScenarioAsync(
        string targetHost,
        bool configureAltSni = false)
    {
        var proxyPort = GetFreeTcpPort();
        var upstreamPort = GetFreeTcpPort();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var dataDirectory = Path.Combine(Path.GetTempPath(), $"mdrava-tls-{Guid.NewGuid():N}");

        try
        {
            TestCertificates.WriteSelfSignedPfx(Path.Combine(dataDirectory, "certs", "home.pfx"), "home.test");
            if (configureAltSni)
            {
                TestCertificates.WriteSelfSignedPfx(Path.Combine(dataDirectory, "certs", "alt.pfx"), "alt.test");
                WriteDualCertificateOperationalConfig(dataDirectory);
                WriteDualCertificateHttpsSite(dataDirectory, proxyPort, upstreamPort);
            }
            else
            {
                ConfigurationTests.WriteHttpsSite(dataDirectory, "tls.json", proxyPort, upstreamPort, "home-cert");
                ConfigurationTests.WriteOperationalConfig(
                    dataDirectory,
                    certificateId: "home-cert",
                    certificatePath: "certs/home.pfx");
            }

            var upstreamTask = RunSingleResponseUpstreamAsync(upstreamPort, timeout.Token);
            using var host = BuildProxyHost(dataDirectory);
            await host.StartAsync(timeout.Token);

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, proxyPort, timeout.Token);
                await using var tlsStream = new SslStream(client.GetStream(), false, (_, _, _, _) => true);
                await tlsStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = targetHost
                }, timeout.Token);

                var remoteCertificate = new X509Certificate2(tlsStream.RemoteCertificate!);
                var requestBytes = Encoding.ASCII.GetBytes("GET /secure HTTP/1.1\r\nHost: home.test\r\nConnection: close\r\n\r\n");
                await tlsStream.WriteAsync(requestBytes, timeout.Token);
                var clientResponse = await ReadToEndAsync(tlsStream, timeout.Token);
                var upstreamRequest = await upstreamTask.WaitAsync(timeout.Token);
                var metrics = host.Services.GetRequiredService<ProxyMetrics>().Snapshot();
                return new TlsProxyScenarioResult(clientResponse, upstreamRequest, remoteCertificate.Subject, metrics);
            }
            finally
            {
                await host.StopAsync(CancellationToken.None);
            }
        }
        finally
        {
            DeleteDirectory(dataDirectory);
        }
    }

    private static async Task<PersistentClientScenarioResult> RunPersistentClientScenarioAsync(
        IReadOnlyList<string> clientRequests,
        IReadOnlyList<string> upstreamResponses,
        int maxRequestsPerClientConnection = 100,
        int clientKeepAliveIdleTimeoutMs = 1000,
        bool expectClientCloseAfterLastResponse = false,
        bool readSecondAsRawClose = false,
        bool closeUpstreamAfterEachResponse = false,
        bool useSeparateClients = false)
    {
        var proxyPort = GetFreeTcpPort();
        var upstreamPort = GetFreeTcpPort();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var dataDirectory = Path.Combine(Path.GetTempPath(), $"mdrava-persistent-{Guid.NewGuid():N}");

        try
        {
            ConfigurationTests.WriteSite(dataDirectory, "persistent.json", proxyPort, upstreamPort);
            ConfigurationTests.WriteOperationalConfig(
                dataDirectory,
                clientKeepAliveIdleTimeoutMs: clientKeepAliveIdleTimeoutMs,
                maxRequestsPerClientConnection: maxRequestsPerClientConnection);

            var upstreamTask = RunPersistentScenarioUpstreamAsync(
                upstreamPort,
                upstreamResponses,
                closeUpstreamAfterEachResponse,
                timeout.Token);

            using var host = BuildProxyHost(dataDirectory);
            await host.StartAsync(timeout.Token);

            List<string> clientResponses = [];
            try
            {
                if (useSeparateClients)
                {
                    foreach (var request in clientRequests)
                    {
                        clientResponses.Add(await SendSingleRequestAsync(proxyPort, request, timeout.Token));
                    }
                }
                else
                {
                    using var client = new TcpClient();
                    await client.ConnectAsync(IPAddress.Loopback, proxyPort, timeout.Token);
                    await using var stream = client.GetStream();

                    for (var index = 0; index < clientRequests.Count; index++)
                    {
                        var requestBytes = Encoding.ASCII.GetBytes(clientRequests[index]);
                        await stream.WriteAsync(requestBytes, timeout.Token);
                        if (readSecondAsRawClose && index == 1)
                        {
                            clientResponses.Add(await ReadToEndAsync(stream, timeout.Token));
                            break;
                        }

                        clientResponses.Add(await ReadHttpResponseAsync(stream, timeout.Token));
                    }

                    if (expectClientCloseAfterLastResponse)
                    {
                        await WaitForClientCloseAsync(stream, timeout.Token);
                    }
                }
            }
            finally
            {
                await host.StopAsync(CancellationToken.None);
            }

            var upstreamResult = await upstreamTask.WaitAsync(timeout.Token);
            var metrics = host.Services.GetRequiredService<ProxyMetrics>().Snapshot();
            return new PersistentClientScenarioResult(
                clientResponses,
                upstreamResult.Requests,
                upstreamResult.AcceptedConnections,
                expectClientCloseAfterLastResponse,
                metrics);
        }
        finally
        {
            DeleteDirectory(dataDirectory);
        }
    }

    private static async Task<string> SendSingleRequestAsync(
        int proxyPort,
        string request,
        CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, proxyPort, cancellationToken);
        await using var stream = client.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request), cancellationToken);
        return await ReadToEndAsync(stream, cancellationToken);
    }

    private static async Task WaitForClientCloseAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[1];
        while (true)
        {
            try
            {
                var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0)
                {
                    return;
                }
            }
            catch (IOException)
            {
                return;
            }
        }
    }

    private static async Task<PersistentUpstreamResult> RunPersistentScenarioUpstreamAsync(
        int upstreamPort,
        IReadOnlyList<string> responses,
        bool closeAfterEachResponse,
        CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Loopback, upstreamPort);
        listener.Start();
        List<string> requests = [];
        var acceptedConnections = 0;

        try
        {
            while (requests.Count < responses.Count)
            {
                using var client = await listener.AcceptTcpClientAsync(cancellationToken);
                acceptedConnections++;
                await using var stream = client.GetStream();

                while (requests.Count < responses.Count)
                {
                    var requestText = await ReadHttpRequestAsync(stream, readBody: true, cancellationToken);
                    if (string.IsNullOrEmpty(requestText))
                    {
                        break;
                    }

                    requests.Add(requestText);
                    var response = responses[requests.Count - 1];
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken);
                    await stream.FlushAsync(cancellationToken);

                    if (closeAfterEachResponse || response.Contains("\r\nConnection: close", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                }
            }

            return new PersistentUpstreamResult(requests, acceptedConnections);
        }
        finally
        {
            listener.Stop();
        }
    }

    private static IHost BuildProxyHost(string dataDirectory)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(builder =>
            {
                builder.Sources.Clear();
                builder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Mdrava:DataDirectory"] = dataDirectory
                });
            })
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureServices((context, services) =>
            {
                services.AddProxyDataPlane(context.Configuration);
            })
            .Build();
    }

    private static void WriteDualCertificateOperationalConfig(string dataDirectory)
    {
        var configDirectory = Directory.CreateDirectory(Path.Combine(dataDirectory, "config")).FullName;
        File.WriteAllText(
            Path.Combine(configDirectory, "proxy.json"),
            """
            {
              "certificates": [
                {
                  "id": "home-cert",
                  "format": "pfx",
                  "path": "certs/home.pfx"
                },
                {
                  "id": "alt-cert",
                  "format": "pfx",
                  "path": "certs/alt.pfx"
                }
              ]
            }
            """);
    }

    private static void WriteDualCertificateHttpsSite(string dataDirectory, int proxyPort, int upstreamPort)
    {
        var sites = Directory.CreateDirectory(Path.Combine(dataDirectory, "config", "sites")).FullName;
        File.WriteAllText(
            Path.Combine(sites, "tls.json"),
            $$"""
            {
              "name": "tls",
              "listeners": [
                {
                  "name": "main",
                  "address": "127.0.0.1",
                  "port": {{proxyPort}},
                  "transport": "https",
                  "defaultCertificateId": "home-cert",
                  "sniCertificates": [
                    {
                      "hostName": "alt.test",
                      "certificateId": "alt-cert"
                    }
                  ]
                }
              ],
              "host": "*",
              "pathPrefix": "/",
              "upstreams": [
                {
                  "name": "local-test",
                  "address": "127.0.0.1",
                  "port": {{upstreamPort}}
                }
              ]
            }
            """);
    }

    private static async Task<ProxyMetricsSnapshot> WaitForMetricsAsync(
        ProxyMetrics metrics,
        Func<ProxyMetricsSnapshot, bool> predicate,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var snapshot = metrics.Snapshot();
            if (predicate(snapshot))
            {
                return snapshot;
            }

            await Task.Delay(10, cancellationToken);
        }
    }

    private static void DeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
        }
    }

    private static async Task<string> RunScenarioUpstreamAsync(
        int upstreamPort,
        string upstreamResponse,
        bool readBody,
        bool sendResponse,
        CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Loopback, upstreamPort);
        listener.Start();

        try
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken);
            await using var stream = client.GetStream();
            var requestText = await ReadHttpRequestAsync(stream, readBody, cancellationToken);
            if (sendResponse)
            {
                var responseBytes = Encoding.ASCII.GetBytes(upstreamResponse);
                await stream.WriteAsync(responseBytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            await Task.Delay(300, cancellationToken);
            return requestText;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static string WithConnectionClose(string request)
    {
        var separator = request.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        if (separator < 0 || request.Contains("\r\nConnection:", StringComparison.OrdinalIgnoreCase))
        {
            return request;
        }

        return request.Insert(separator, "\r\nConnection: close");
    }

    private static async Task<string> ReadHttpRequestAsync(
        NetworkStream stream,
        bool readBody,
        CancellationToken cancellationToken)
    {
        var headAndMaybeBody = await ReadRequestHeadAsync(stream, cancellationToken);
        if (!readBody)
        {
            return headAndMaybeBody;
        }

        var separator = headAndMaybeBody.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var head = separator >= 0 ? headAndMaybeBody[..(separator + 4)] : headAndMaybeBody;
        var body = separator >= 0 ? headAndMaybeBody[(separator + 4)..] : "";

        if (head.Contains("Transfer-Encoding: chunked", StringComparison.OrdinalIgnoreCase))
        {
            while (!body.Contains("0\r\n\r\n", StringComparison.Ordinal) && !body.Contains("0\r\nX-Trailer: ok\r\n\r\n", StringComparison.Ordinal))
            {
                body += await ReadSomeAsync(stream, cancellationToken);
            }
        }
        else if (TryGetContentLength(head, out var contentLength))
        {
            while (Encoding.ASCII.GetByteCount(body) < contentLength)
            {
                body += await ReadSomeAsync(stream, cancellationToken);
            }
        }

        return head + body;
    }

    private static async Task<string> ReadSomeAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
        if (bytesRead == 0)
        {
            return "";
        }

        return Encoding.ASCII.GetString(buffer, 0, bytesRead);
    }

    private static bool TryGetContentLength(string head, out int contentLength)
    {
        contentLength = 0;
        foreach (var line in head.Split("\r\n"))
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                return int.TryParse(line["Content-Length:".Length..].Trim(), out contentLength);
            }
        }

        return false;
    }

    private static async Task<string> ReadRequestHeadAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var total = 0;

        while (total < buffer.Length)
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            total += bytesRead;
            if (ContainsRequestHeadTerminator(buffer.AsSpan(0, total)))
            {
                break;
            }
        }

        return Encoding.ASCII.GetString(buffer, 0, total);
    }

    private static async Task<string> ReadHttpResponseAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var head = await ReadResponseHeadAsync(stream, cancellationToken);
        if (TryGetContentLength(head, out var contentLength))
        {
            return head + await ReadExactTextAsync(stream, contentLength, cancellationToken);
        }

        if (head.Contains("Transfer-Encoding: chunked", StringComparison.OrdinalIgnoreCase))
        {
            var body = new StringBuilder();
            while (true)
            {
                var line = await ReadLineAsync(stream, cancellationToken);
                body.Append(line);
                if (line == "0\r\n")
                {
                    while (true)
                    {
                        var trailerLine = await ReadLineAsync(stream, cancellationToken);
                        body.Append(trailerLine);
                        if (trailerLine == "\r\n")
                        {
                            return head + body;
                        }
                    }
                }

                var sizeText = line[..^2].Split(';')[0];
                var size = Convert.ToInt32(sizeText, 16);
                body.Append(await ReadExactTextAsync(stream, size + 2, cancellationToken));
            }
        }

        return head;
    }

    private static async Task<string> ReadResponseHeadAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var total = 0;

        while (total < buffer.Length)
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(total, 1), cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            total += bytesRead;
            if (ContainsRequestHeadTerminator(buffer.AsSpan(0, total)))
            {
                break;
            }
        }

        return Encoding.ASCII.GetString(buffer, 0, total);
    }

    private static async Task<string> ReadLineAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        List<byte> bytes = [];
        var previous = (byte)0;
        var buffer = new byte[1];

        while (true)
        {
            var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            var current = buffer[0];
            bytes.Add(current);
            if (previous == (byte)'\r' && current == (byte)'\n')
            {
                break;
            }

            previous = current;
        }

        return Encoding.ASCII.GetString(bytes.ToArray());
    }

    private static async Task<string> ReadExactTextAsync(
        Stream stream,
        int length,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var total = 0;
        while (total < length)
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(total, length - total), cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            total += bytesRead;
        }

        return Encoding.ASCII.GetString(buffer, 0, total);
    }

    private static async Task<string> ReadToEndAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var builder = new StringBuilder();

        while (true)
        {
            int bytesRead;
            try
            {
                bytesRead = await stream.ReadAsync(buffer, cancellationToken);
            }
            catch (IOException)
            {
                break;
            }
            if (bytesRead == 0)
            {
                break;
            }

            builder.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));
        }

        return builder.ToString();
    }

    private static bool ContainsRequestHeadTerminator(ReadOnlySpan<byte> bytes)
    {
        for (var index = 3; index < bytes.Length; index++)
        {
            if (bytes[index - 3] == (byte)'\r'
                && bytes[index - 2] == (byte)'\n'
                && bytes[index - 1] == (byte)'\r'
                && bytes[index] == (byte)'\n')
            {
                return true;
            }
        }

        return false;
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private sealed record ProxyScenarioResult(
        string ClientResponse,
        string UpstreamRequest,
        ProxyMetricsSnapshot Metrics);

    private sealed record TlsProxyScenarioResult(
        string ClientResponse,
        string UpstreamRequest,
        string RemoteCertificateSubject,
        ProxyMetricsSnapshot Metrics);

    private sealed record PersistentUpstreamResult(
        IReadOnlyList<string> Requests,
        int AcceptedConnections);

    private sealed record PersistentClientScenarioResult(
        IReadOnlyList<string> ClientResponses,
        IReadOnlyList<string> UpstreamRequests,
        int UpstreamAcceptedConnections,
        bool ClientClosedAfterLastResponse,
        ProxyMetricsSnapshot Metrics);
}
