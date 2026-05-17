#pragma warning disable CA1416
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using MDRAVA.API.Proxy.Configuration;
using MDRAVA.API.Proxy.Configuration.Loading;
using MDRAVA.API.Proxy.Configuration.Paths;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Connections;
using MDRAVA.API.Proxy.Health;
using MDRAVA.API.Proxy.Hosting;
using MDRAVA.API.Proxy.Http3;
using MDRAVA.API.Proxy.Metrics;
using MDRAVA.API.Proxy.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MDRAVA.Tests;

internal static class UpstreamHttp3Tests
{
    private static readonly SslApplicationProtocol Http3Alpn = new("h3");

    public static void Http3UpstreamRequiresHttps()
    {
        var validation = new ProxyOptionsValidator().Validate(
            null,
            OptionsWithUpstream("http", RuntimeUpstreamProtocol.Http3));

        AssertEx.True(validation.Failed);
        AssertEx.True(AssertEx.NotNull(validation.Failures).Any(static failure =>
            failure.Contains("HTTP/3 upstreams require scheme 'https'", StringComparison.Ordinal)),
            string.Join("; ", validation.Failures ?? []));
    }

    public static async Task Http3UpstreamConfigParsesAndValidates()
    {
        using var temp = TemporaryDirectory.Create();
        ConfigurationTests.WriteCustomSite(
            temp.Path,
            "upstream-h3.json",
            SiteJson(proxyPort: 18080, upstreamPort: 18443));

        var result = await CreateLoader(temp.Path).LoadAsync(CancellationToken.None);

        AssertEx.True(result.Succeeded, string.Join("; ", result.Errors));
        var upstream = AssertEx.NotNull(result.Snapshot).Routes[0].Upstreams[0];
        AssertEx.Equal(RuntimeUpstreamProtocol.Http3, upstream.Protocol);
        AssertEx.Equal("https", upstream.Scheme);
    }

    public static void PoolKeyDiffersForHttp1Http2AndHttp3()
    {
        var http1 = Upstream(5001, RuntimeUpstreamProtocol.Http1);
        var http2 = Upstream(5001, RuntimeUpstreamProtocol.Http2);
        var http3 = Upstream(5001, RuntimeUpstreamProtocol.Http3);

        AssertEx.False(string.Equals(UpstreamConnectionPool.GetKey(http1), UpstreamConnectionPool.GetKey(http3), StringComparison.Ordinal));
        AssertEx.False(string.Equals(UpstreamConnectionPool.GetKey(http2), UpstreamConnectionPool.GetKey(http3), StringComparison.Ordinal));
    }

    public static void PoolKeyIncludesHttp3SniAndValidation()
    {
        var first = Upstream(5001, RuntimeUpstreamProtocol.Http3, validateCertificate: true, sniHost: "one.test");
        var second = Upstream(5001, RuntimeUpstreamProtocol.Http3, validateCertificate: true, sniHost: "two.test");
        var third = Upstream(5001, RuntimeUpstreamProtocol.Http3, validateCertificate: false, sniHost: "one.test");

        AssertEx.False(string.Equals(UpstreamConnectionPool.GetKey(first), UpstreamConnectionPool.GetKey(second), StringComparison.Ordinal));
        AssertEx.False(string.Equals(UpstreamConnectionPool.GetKey(first), UpstreamConnectionPool.GetKey(third), StringComparison.Ordinal));
    }

    public static async Task Http3UpstreamProxyMapsHeadersQueryAndResponse()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        var result = await RunProxyScenarioAsync(
            "/api/users?id=1",
            200,
            [("content-length", "7"), ("x-upstream", "h3")],
            Encoding.ASCII.GetBytes("h3-body"),
            requestHeaders: "Connection: keep-alive\r\nKeep-Alive: timeout=5\r\n");

        AssertEx.True(result.ClientResponse.Contains("200 OK", StringComparison.Ordinal), result.ClientResponse);
        AssertEx.True(result.ClientResponse.EndsWith("h3-body", StringComparison.Ordinal), result.ClientResponse);
        AssertEx.Equal("GET", result.Upstream.RequestHeaders[":method"]);
        AssertEx.Equal("https", result.Upstream.RequestHeaders[":scheme"]);
        AssertEx.Equal("/api/users?id=1", result.Upstream.RequestHeaders[":path"]);
        AssertEx.False(result.Upstream.RequestHeaders.ContainsKey("connection"));
        AssertEx.False(result.Upstream.RequestHeaders.ContainsKey("keep-alive"));
    }

    public static async Task Http3UpstreamForwardsRequestBody()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        var result = await RunProxyScenarioAsync(
            "/submit",
            201,
            [("content-length", "2")],
            Encoding.ASCII.GetBytes("ok"),
            method: "POST",
            requestBody: "hello h3");

        AssertEx.True(result.ClientResponse.Contains("201 Created", StringComparison.Ordinal), result.ClientResponse);
        AssertEx.Equal("POST", result.Upstream.RequestHeaders[":method"]);
        AssertEx.Equal("8", result.Upstream.RequestHeaders["content-length"]);
        AssertEx.Equal("hello h3", Encoding.ASCII.GetString(result.Upstream.RequestBody));
    }

    public static async Task Http3HealthCheckUsesH3()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        var port = GetFreeUdpPort();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = RunSingleHttp3UpstreamAsync(
            port,
            204,
            [],
            [],
            timeout.Token);
        var client = new UpstreamHealthCheckClient(new UpstreamConnectionFactory());

        var healthy = await client.CheckAsync(Route([Upstream(port, RuntimeUpstreamProtocol.Http3)]), Upstream(port, RuntimeUpstreamProtocol.Http3), timeout.Token);
        var observation = await server.WaitAsync(timeout.Token);

        AssertEx.True(healthy.Healthy, healthy.Result);
        AssertEx.True(healthy.Result.Contains("HTTP/3 204", StringComparison.Ordinal), healthy.Result);
        AssertEx.Equal("GET", observation.RequestHeaders[":method"]);
        AssertEx.Equal("/health", observation.RequestHeaders[":path"]);
    }

    public static async Task MetricsIncludeUpstreamHttp3Counters()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        var result = await RunProxyScenarioAsync(
            "/metrics",
            200,
            [("content-length", "2")],
            Encoding.ASCII.GetBytes("ok"));

        AssertEx.True(result.Metrics.UpstreamHttp3Requests >= 1, result.Metrics.UpstreamHttp3Requests.ToString());
        AssertEx.True(result.Metrics.UpstreamHttp3ConnectionAttempts >= 1, result.Metrics.UpstreamHttp3ConnectionAttempts.ToString());
        AssertEx.True(result.Metrics.UpstreamHttp3ConnectionSuccesses >= 1, result.Metrics.UpstreamHttp3ConnectionSuccesses.ToString());
    }

    private static async Task<ProxyScenarioResult> RunProxyScenarioAsync(
        string target,
        int statusCode,
        IReadOnlyList<(string Name, string Value)> responseHeaders,
        byte[] responseBody,
        string requestHeaders = "",
        string method = "GET",
        string requestBody = "")
    {
        var proxyPort = GetFreeTcpPort();
        var upstreamPort = GetFreeUdpPort();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var temp = TemporaryDirectory.Create();
        ConfigurationTests.WriteCustomSite(temp.Path, "upstream-h3.json", SiteJson(proxyPort, upstreamPort));
        var upstreamTask = RunSingleHttp3UpstreamAsync(
            upstreamPort,
            statusCode,
            responseHeaders,
            responseBody,
            timeout.Token);
        using var host = BuildProxyHost(temp.Path);
        await host.StartAsync(timeout.Token);

        try
        {
            var requestBodyBytes = Encoding.ASCII.GetBytes(requestBody);
            var contentLengthHeader = requestBodyBytes.Length > 0
                ? $"Content-Length: {requestBodyBytes.Length}\r\n"
                : "";
            var request = $"{method} {target} HTTP/1.1\r\nHost: home.test\r\n{requestHeaders}{contentLengthHeader}Connection: close\r\n\r\n{requestBody}";
            var first = await SendSingleRequestAsync(proxyPort, request, timeout.Token);
            var upstream = await upstreamTask.WaitAsync(timeout.Token);
            var metrics = host.Services.GetRequiredService<ProxyMetrics>().Snapshot();
            return new ProxyScenarioResult(first, upstream, metrics);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    private static string SiteJson(int proxyPort, int upstreamPort)
    {
        return $$"""
        {
          "name": "upstream-h3",
          "listeners": [
            {
              "name": "main",
              "address": "127.0.0.1",
              "port": {{proxyPort}}
            }
          ],
          "host": "*",
          "routes": [
            {
              "name": "app",
              "pathPrefix": "/",
              "action": "proxy",
              "upstreams": [
                {
                  "name": "h3-upstream",
                  "scheme": "https",
                  "protocol": "http3",
                  "address": "127.0.0.1",
                  "port": {{upstreamPort}},
                  "upstreamTls": {
                    "validateCertificate": false,
                    "sniHost": "upstream.test"
                  }
                }
              ]
            }
          ]
        }
        """;
    }

    private static async Task<Http3UpstreamObservation> RunSingleHttp3UpstreamAsync(
        int port,
        int statusCode,
        IReadOnlyList<(string Name, string Value)> responseHeaders,
        byte[] responseBody,
        CancellationToken cancellationToken)
    {
        await using var listener = await CreateQuicListenerAsync(port, cancellationToken);
        try
        {
            await using var connection = await listener.AcceptConnectionAsync(cancellationToken);
            while (true)
            {
                var stream = await connection.AcceptInboundStreamAsync(cancellationToken);
                if (stream.Type != QuicStreamType.Bidirectional)
                {
                    _ = DrainAsync(stream, cancellationToken);
                    continue;
                }

                await using var ownedStream = stream;
                var observation = await ReadRequestAsync(stream, cancellationToken);
                await WriteResponseAsync(stream, statusCode, responseHeaders, responseBody, cancellationToken);
                return observation;
            }
        }
        catch (Exception exception) when (exception is AuthenticationException or IOException or QuicException)
        {
            return new Http3UpstreamObservation(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), [], exception.GetType().Name);
        }
    }

    private static async ValueTask<QuicListener> CreateQuicListenerAsync(
        int port,
        CancellationToken cancellationToken)
    {
        var certificate = CreateServerCertificate("upstream.test");
        return await QuicListener.ListenAsync(
            new QuicListenerOptions
            {
                ListenEndPoint = new IPEndPoint(IPAddress.Loopback, port),
                ApplicationProtocols = [Http3Alpn],
                ConnectionOptionsCallback = (_, _, _) =>
                    ValueTask.FromResult(new QuicServerConnectionOptions
                    {
                        ServerAuthenticationOptions = new SslServerAuthenticationOptions
                        {
                            ServerCertificate = certificate,
                            EnabledSslProtocols = SslProtocols.Tls13,
                            ApplicationProtocols = [Http3Alpn],
                            CertificateRevocationCheckMode = X509RevocationMode.NoCheck
                        },
                        MaxInboundBidirectionalStreams = 4,
                        MaxInboundUnidirectionalStreams = 4,
                        IdleTimeout = TimeSpan.FromSeconds(5),
                        HandshakeTimeout = TimeSpan.FromSeconds(5),
                        DefaultCloseErrorCode = 0x100,
                        DefaultStreamErrorCode = 0x100
                    })
            },
            cancellationToken);
    }

    private static async ValueTask<Http3UpstreamObservation> ReadRequestAsync(
        QuicStream stream,
        CancellationToken cancellationToken)
    {
        var requestHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var requestBody = new MemoryStream();
        while (true)
        {
            var frame = await ReadFrameAsync(stream, cancellationToken);
            if (frame.EndStream)
            {
                return new Http3UpstreamObservation(requestHeaders, requestBody.ToArray(), null);
            }

            if (frame.Type == Http3PreviewCodec.HeadersFrame)
            {
                if (!Http3PreviewCodec.TryDecodeHeaderBlock(
                        frame.Payload.Span,
                        maxHeaderBytes: 64 * 1024,
                        out var headers,
                        out var reason))
                {
                    return new Http3UpstreamObservation(requestHeaders, requestBody.ToArray(), reason);
                }

                foreach (var header in headers)
                {
                    requestHeaders[header.Name] = header.Value;
                }

                continue;
            }

            if (frame.Type == Http3PreviewCodec.DataFrame)
            {
                requestBody.Write(frame.Payload.Span);
                continue;
            }

            return new Http3UpstreamObservation(requestHeaders, requestBody.ToArray(), "unexpected_frame");
        }
    }

    private static async ValueTask WriteResponseAsync(
        QuicStream stream,
        int statusCode,
        IReadOnlyList<(string Name, string Value)> responseHeaders,
        ReadOnlyMemory<byte> responseBody,
        CancellationToken cancellationToken)
    {
        List<Http1HeaderField> headers = [new(":status", statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture))];
        foreach (var header in responseHeaders)
        {
            headers.Add(new Http1HeaderField(header.Name, header.Value));
        }

        var block = Http3PreviewCodec.EncodeHeaderBlock(headers);
        using var head = new MemoryStream();
        Http3PreviewCodec.WriteFrame(head, Http3PreviewCodec.HeadersFrame, block);
        await stream.WriteAsync(head.ToArray(), completeWrites: responseBody.Length == 0, cancellationToken);
        if (responseBody.Length > 0)
        {
            using var body = new MemoryStream();
            Http3PreviewCodec.WriteFrame(body, Http3PreviewCodec.DataFrame, responseBody.Span);
            await stream.WriteAsync(body.ToArray(), completeWrites: true, cancellationToken);
        }
    }

    private static async Task DrainAsync(
        QuicStream stream,
        CancellationToken cancellationToken)
    {
        await using var ownedStream = stream;
        var buffer = new byte[256];
        while (await stream.ReadAsync(buffer, cancellationToken) > 0)
        {
        }
    }

    private static async ValueTask<Http3FrameReadResult> ReadFrameAsync(
        QuicStream stream,
        CancellationToken cancellationToken)
    {
        var type = await ReadVarIntAsync(stream, cancellationToken);
        if (!type.Success)
        {
            return Http3FrameReadResult.End;
        }

        var length = await ReadVarIntAsync(stream, cancellationToken);
        if (!length.Success || length.Value < 0 || length.Value > 1024 * 1024)
        {
            throw new IOException("Invalid HTTP/3 frame length.");
        }

        var payload = length.Value == 0
            ? []
            : await ReadExactAsync(stream, (int)length.Value, cancellationToken);
        return new Http3FrameReadResult(false, type.Value, payload);
    }

    private static async ValueTask<Http3VarIntReadResult> ReadVarIntAsync(
        QuicStream stream,
        CancellationToken cancellationToken)
    {
        var first = await ReadExactAsync(stream, 1, cancellationToken, allowEnd: true);
        if (first.Length == 0)
        {
            return Http3VarIntReadResult.Failure;
        }

        var length = 1 << (first[0] >> 6);
        var value = first[0] & 0x3f;
        if (length == 1)
        {
            return new Http3VarIntReadResult(true, value);
        }

        var rest = await ReadExactAsync(stream, length - 1, cancellationToken);
        foreach (var next in rest)
        {
            value = (value << 8) | next;
        }

        return new Http3VarIntReadResult(true, value);
    }

    private static async ValueTask<byte[]> ReadExactAsync(
        QuicStream stream,
        int length,
        CancellationToken cancellationToken,
        bool allowEnd = false)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken);
            if (read == 0)
            {
                return allowEnd && offset == 0
                    ? []
                    : throw new IOException("Connection closed while reading HTTP/3 data.");
            }

            offset += read;
        }

        return buffer;
    }

    private static ProxyOptions OptionsWithUpstream(string scheme, string protocol)
    {
        return new ProxyOptions
        {
            Listeners =
            [
                new ListenerOptions
                {
                    Name = "main",
                    Address = "127.0.0.1",
                    Port = 8080
                }
            ],
            Routes =
            [
                new ProxyRouteOptions
                {
                    Name = "proxy",
                    Host = "*",
                    PathPrefix = "/",
                    Upstreams =
                    [
                        new UpstreamOptions
                        {
                            Name = "bad",
                            Scheme = scheme,
                            Protocol = protocol,
                            Address = "127.0.0.1",
                            Port = 5000
                        }
                    ]
                }
            ]
        };
    }

    private static RuntimeRoute Route(IReadOnlyList<RuntimeUpstream> upstreams)
    {
        return new RuntimeRoute(
            "test",
            "*",
            "/",
            RuntimeRouteAction.Proxy,
            "round-robin",
            new RuntimeHealthCheckOptions(
                true,
                "/health",
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2),
                1,
                1),
            upstreams,
            new RuntimeHttpsRedirectPolicy(false, 308, null),
            new RuntimeCanonicalHostPolicy(false, "", 308),
            RuntimeHeaderPolicy.Empty,
            new RuntimePathRewritePolicy("", "", ""),
            new RuntimeRedirectPolicy(308, "", "", true),
            new RuntimeStaticResponse(200, "text/plain; charset=utf-8", ""),
            new RuntimeMaintenancePolicy(false, null, "text/plain; charset=utf-8", "Service Unavailable"),
            RuntimeCachePolicy.Disabled,
            new RuntimeRouteResolvedOptions(
                100L * 1024 * 1024,
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30),
                true));
    }

    private static RuntimeUpstream Upstream(
        int port,
        string protocol,
        bool validateCertificate = false,
        string sniHost = "upstream.test")
    {
        return new RuntimeUpstream(
            "test",
            $"upstream-{port}",
            "https",
            protocol,
            "127.0.0.1",
            port,
            1,
            new RuntimeUpstreamTlsOptions(validateCertificate, sniHost));
    }

    private static ProxyConfigurationLoader CreateLoader(string dataDirectory)
    {
        var provider = new MdravaDataDirectoryProvider(Options.Create(new MdravaDataDirectoryOptions
        {
            DataDirectory = dataDirectory
        }));

        return new ProxyConfigurationLoader(
            provider,
            new ProxyOptionsValidator(),
            new ProxyDataDirectoryBootstrapper(provider),
            new SiteConfigurationParser(),
            NullLogger<ProxyConfigurationLoader>.Instance);
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

    private static async Task<string> ReadToEndAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[1024];
        while (true)
        {
            var bytesRead = await stream.ReadAsync(chunk, cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            buffer.Write(chunk, 0, bytesRead);
        }

        return Encoding.ASCII.GetString(buffer.ToArray());
    }

    private static X509Certificate2 CreateServerCertificate(string subjectName)
    {
        var pfxBytes = TestCertificates.CreateSelfSignedPfxBytes(subjectName);
        return X509CertificateLoader.LoadPkcs12(
            pfxBytes,
            ReadOnlySpan<char>.Empty,
            X509KeyStorageFlags.UserKeySet);
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

    private static int GetFreeUdpPort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    private sealed record ProxyScenarioResult(
        string ClientResponse,
        Http3UpstreamObservation Upstream,
        ProxyMetricsSnapshot Metrics);

    private sealed record Http3UpstreamObservation(
        IReadOnlyDictionary<string, string> RequestHeaders,
        byte[] RequestBody,
        string? Error);

    private readonly record struct Http3FrameReadResult(
        bool EndStream,
        long Type,
        ReadOnlyMemory<byte> Payload)
    {
        public static Http3FrameReadResult End { get; } = new(true, 0, ReadOnlyMemory<byte>.Empty);
    }

    private readonly record struct Http3VarIntReadResult(bool Success, long Value)
    {
        public static Http3VarIntReadResult Failure { get; } = new(false, 0);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"mdrava-upstream-h3-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
