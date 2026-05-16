using System.Net;
using System.Net.Sockets;
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
                var requestBytes = Encoding.ASCII.GetBytes("GET /smoke HTTP/1.1\r\nHost: smoke.test\r\n\r\n");
                await stream.WriteAsync(requestBytes, timeout.Token);

                var responseText = await ReadToEndAsync(stream, timeout.Token);

                AssertEx.True(responseText.Contains("200 OK", StringComparison.Ordinal), responseText);
                AssertEx.True(responseText.EndsWith("proxied", StringComparison.Ordinal), responseText);

                var upstreamRequest = await upstreamTask.WaitAsync(timeout.Token);
                AssertEx.True(upstreamRequest.StartsWith("GET /smoke HTTP/1.1", StringComparison.Ordinal), upstreamRequest);
                AssertEx.True(upstreamRequest.Contains("Connection: close", StringComparison.OrdinalIgnoreCase), upstreamRequest);
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
            "GET /headers HTTP/1.1\r\nHost: header.test\r\nConnection: x-private\r\nX-Private: secret\r\nKeep-Alive: timeout=5\r\n\r\n",
            "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok",
            readBodyFromUpstreamRequest: false);

        AssertEx.False(result.UpstreamRequest.Contains("X-Private", StringComparison.OrdinalIgnoreCase), result.UpstreamRequest);
        AssertEx.False(result.UpstreamRequest.Contains("Keep-Alive", StringComparison.OrdinalIgnoreCase), result.UpstreamRequest);
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
                var requestBytes = Encoding.ASCII.GetBytes(clientRequest);
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

    private static async Task<string> ReadToEndAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var builder = new StringBuilder();

        while (true)
        {
            var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
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
}
