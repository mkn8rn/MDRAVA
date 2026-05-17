#pragma warning disable CA1416
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using MDRAVA.API.Proxy.Configuration.Loading;
using MDRAVA.API.Proxy.Configuration.Paths;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Configuration.Storage;
using MDRAVA.API.Proxy.Configuration;
using MDRAVA.API.Proxy.Hosting;
using MDRAVA.API.Proxy.Http3;
using MDRAVA.API.Proxy.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MDRAVA.Tests;

internal static class ClientHttp3PreviewTests
{
    private static readonly SslApplicationProtocol Http3Alpn = new("h3");

    public static void Http3PreviewDisabledByDefault()
    {
        var listener = new RuntimeListener(
            "main",
            "127.0.0.1",
            8443,
            true,
            RuntimeListenerTransport.Https,
            "default",
            [],
            512,
            32 * 1024,
            32 * 1024,
            1024,
            64 * 1024);

        AssertEx.False(listener.Http3PreviewConfigured);
        AssertEx.False(listener.Http3.EnabledForTraffic);
        AssertEx.Equal("not_configured", listener.Http3.DisabledReason);
    }

    public static void Http3PreviewRequiresExperimentalGate()
    {
        var validation = new ProxyOptionsValidator().Validate(
            null,
            new ProxyOptions
            {
                Listeners =
                [
                    new ListenerOptions
                    {
                        Name = "main",
                        Address = "127.0.0.1",
                        Port = 8443,
                        Transport = "https",
                        Protocols = "http3Preview",
                        DefaultCertificateId = "default"
                    }
                ],
                Routes =
                [
                    new ProxyRouteOptions
                    {
                        Name = "static",
                        Host = "*",
                        PathPrefix = "/",
                        Action = "staticResponse"
                    }
                ]
            });

        AssertEx.True(validation.Failed);
        AssertEx.True(AssertEx.NotNull(validation.Failures).Any(static failure => failure.Contains("ExperimentalHttp3", StringComparison.Ordinal)));
    }

    public static void QuicListenerIdentityIsSeparateFromTcpIdentity()
    {
        var listener = PreviewListener("http1AndHttp2AndHttp3Preview", experimental: true);
        var tcp = listener.Identity;
        var quic = AssertEx.NotNull(listener.QuicIdentity);

        AssertEx.Equal("main", tcp.Key);
        AssertEx.Equal("main|quic", quic.Key);
        AssertEx.False(string.Equals(tcp.BindKey, quic.BindKey, StringComparison.Ordinal));
    }

    public static async Task FailedQuicListenerStartDoesNotBreakTcpListener()
    {
        using var temp = TemporaryDirectory.Create();
        var port = GetFreeTcpUdpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3Site(temp.Path, port, "http1AndHttp3Preview", staticBody: "unused");
        using var host = BuildProxyHost(
            temp.Path,
            services => services.AddSingleton<IHttp3QuicListenerFactory, FailingQuicListenerFactory>());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await host.StartAsync(timeout.Token);
        try
        {
            var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
            await WaitForListenerAsync(runtime, "main", "tcp", ProxyListenerState.Active, timeout.Token);
            await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Failed, timeout.Token);
            var snapshot = runtime.Snapshot();

            AssertEx.True(snapshot.IsRunning);
            AssertEx.True(snapshot.Listeners.Any(static listener => listener.Kind == "tcp" && listener.State == ProxyListenerState.Active));
            AssertEx.True(snapshot.Listeners.Any(static listener => listener.Kind == "quic" && listener.State == ProxyListenerState.Failed));
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    public static async Task SuccessfulReloadCanAddAndRemovePreviewQuicListener()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var temp = TemporaryDirectory.Create();
        var port = GetFreeTcpUdpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3Site(temp.Path, port, "http1", staticBody: "unused", experimental: false);
        using var host = BuildProxyHost(temp.Path);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await host.StartAsync(timeout.Token);
        try
        {
            var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
            await WaitForListenerAsync(runtime, "main", "tcp", ProxyListenerState.Active, timeout.Token);

            WriteHttp3Site(temp.Path, port, "http1AndHttp3Preview", staticBody: "unused");
            var add = await host.Services.GetRequiredService<IProxyConfigurationReloadService>().ReloadAsync(timeout.Token);
            await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);

            WriteHttp3Site(temp.Path, port, "http1", staticBody: "unused", experimental: false);
            var remove = await host.Services.GetRequiredService<IProxyConfigurationReloadService>().ReloadAsync(timeout.Token);
            await WaitForNoListenerAsync(runtime, "main", "quic", timeout.Token);

            AssertEx.True(add.Succeeded, string.Join("; ", add.Errors));
            AssertEx.True(remove.Succeeded, string.Join("; ", remove.Errors));
            AssertEx.True(AssertEx.NotNull(add.ListenerReload).Added >= 1);
            AssertEx.True(AssertEx.NotNull(remove.ListenerReload).Removed >= 1);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    public static async Task FailedReloadPreservesOldPreviewQuicListenerSet()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var temp = TemporaryDirectory.Create();
        var port = GetFreeTcpUdpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3Site(temp.Path, port, "http1AndHttp3Preview", staticBody: "live");
        using var host = BuildProxyHost(temp.Path);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await host.StartAsync(timeout.Token);
        try
        {
            var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
            var before = await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);
            File.WriteAllText(Path.Combine(temp.Path, "config", "sites", "broken.json"), "{ nope");

            var reload = await host.Services.GetRequiredService<IProxyConfigurationReloadService>().ReloadAsync(timeout.Token);
            var after = await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);

            AssertEx.False(reload.Succeeded);
            AssertEx.Equal(before.StartedAtUtc, after.StartedAtUtc);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    public static void StatusAndEffectiveConfigMarkHttp3AsExperimentalPreview()
    {
        var snapshot = ProxyConfigurationMapper.ToRuntimeSnapshot(
            new ProxyOptions
            {
                Listeners =
                [
                    new ListenerOptions
                    {
                        Name = "main",
                        Address = "127.0.0.1",
                        Port = 8443,
                        Transport = "https",
                        Protocols = "http3Preview",
                        ExperimentalHttp3 = true,
                        DefaultCertificateId = "default"
                    }
                ],
                Routes =
                [
                    new ProxyRouteOptions
                    {
                        Name = "static",
                        Host = "*",
                        PathPrefix = "/",
                        Action = "staticResponse"
                    }
                ]
            },
            new ProxyOperationalOptions(),
            new Dictionary<string, RuntimeCertificate>(StringComparer.OrdinalIgnoreCase),
            1,
            DateTimeOffset.UtcNow,
            "memory",
            [],
            new ProxyConfigurationDiscovery(
                new ProxyFilesystemLayout("data", "config", "sites", "logs", "certs", "state", "proxy.json"),
                [],
                [],
                []));

        var projection = ProxyConfigurationMapper.ToProjection(snapshot);

        AssertEx.Equal("preview", projection.Http3.Configured);
        AssertEx.True(projection.Http3.EnabledForTraffic);
        AssertEx.Equal("preview_enabled", projection.Http3.DisabledReason);
        AssertEx.True(snapshot.Listeners[0].Http3.ExperimentalGateEnabled);
    }

    public static async Task MinimalHttp3GetCanReachGeneratedRoute()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var result = await RunHttp3GeneratedRouteScenarioAsync("GET", "/hello?x=1", "hello-h3");

        AssertEx.Equal("200", HeaderValue(result.Headers, ":status"));
        AssertEx.Equal("hello-h3", result.Body);
        AssertEx.True(result.Metrics.Http3AcceptedConnections >= 1);
        AssertEx.True(result.Metrics.Http3Requests >= 1);
    }

    public static async Task HeadReturnsHeadersWithoutBody()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var result = await RunHttp3GeneratedRouteScenarioAsync("HEAD", "/head", "head-body");

        AssertEx.Equal("200", HeaderValue(result.Headers, ":status"));
        AssertEx.Equal("9", HeaderValue(result.Headers, "content-length"));
        AssertEx.Equal("", result.Body);
    }

    public static async Task UnsupportedConnectIsRejected()
    {
        if (!QuicListener.IsSupported || !QuicConnection.IsSupported)
        {
            return;
        }

        using var result = await RunHttp3GeneratedRouteScenarioAsync("CONNECT", "/tunnel", "unused");

        AssertEx.Equal("501", HeaderValue(result.Headers, ":status"));
        AssertEx.True(result.Metrics.Http3RejectedRequests.ContainsKey("connect_unsupported"));
    }

    public static void MalformedPseudoHeadersAreRejected()
    {
        var headers = new[]
        {
            new Http1HeaderField(":method", "GET"),
            new Http1HeaderField(":method", "HEAD"),
            new Http1HeaderField(":scheme", "https"),
            new Http1HeaderField(":authority", "localhost"),
            new Http1HeaderField(":path", "/")
        };

        var ok = Http3PreviewRequestTranslator.TryBuildRequest(
            headers,
            PreviewListener("http3Preview", experimental: true),
            out _,
            out var reason);

        AssertEx.False(ok);
        AssertEx.Equal("invalid_pseudo_header", reason);
    }

    public static void MetricsIncludeHttp3PreviewCounters()
    {
        var metrics = new ProxyMetrics();
        metrics.QuicListenerStarted();
        metrics.Http3ConnectionAccepted();
        metrics.Http3RequestReceived();
        metrics.Http3RequestRejected("proxy_not_implemented");
        metrics.Http3ProtocolError("invalid_frame");
        metrics.SetActiveQuicListeners(1);
        var snapshot = metrics.Snapshot();

        AssertEx.Equal(1L, snapshot.QuicListenerStartSuccesses);
        AssertEx.Equal(1L, snapshot.Http3AcceptedConnections);
        AssertEx.Equal(1L, snapshot.Http3Requests);
        AssertEx.Equal(1L, snapshot.Http3RejectedRequests["proxy_not_implemented"]);
        AssertEx.Equal(1L, snapshot.Http3ProtocolErrors["invalid_frame"]);
        AssertEx.Equal(1L, snapshot.ActiveQuicListeners);
    }

    private static async Task<Http3ScenarioResult> RunHttp3GeneratedRouteScenarioAsync(
        string method,
        string target,
        string body)
    {
        var temp = TemporaryDirectory.Create();
        var port = GetFreeTcpUdpPort();
        WriteCertificateConfig(temp.Path);
        WriteHttp3Site(temp.Path, port, "http3Preview", body);
        var host = BuildProxyHost(temp.Path);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StartAsync(timeout.Token);

        var runtime = host.Services.GetRequiredService<ProxyRuntimeState>();
        await WaitForListenerAsync(runtime, "main", "quic", ProxyListenerState.Active, timeout.Token);
        var response = await SendHttp3RequestAsync(port, method, target, timeout.Token);
        var metrics = host.Services.GetRequiredService<ProxyMetrics>().Snapshot();
        return new Http3ScenarioResult(temp, host, response.Headers, response.Body, metrics);
    }

    private static async Task<Http3Response> SendHttp3RequestAsync(
        int port,
        string method,
        string target,
        CancellationToken cancellationToken)
    {
        await using var connection = await QuicConnection.ConnectAsync(
            new QuicClientConnectionOptions
            {
                RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, port),
                ClientAuthenticationOptions = new SslClientAuthenticationOptions
                {
                    TargetHost = "localhost",
                    ApplicationProtocols = [Http3Alpn],
                    RemoteCertificateValidationCallback = static (_, _, _, _) => true
                },
                MaxInboundBidirectionalStreams = 4,
                MaxInboundUnidirectionalStreams = 4,
                IdleTimeout = TimeSpan.FromSeconds(5),
                HandshakeTimeout = TimeSpan.FromSeconds(5),
                DefaultCloseErrorCode = 0x100,
                DefaultStreamErrorCode = 0x100
            },
            cancellationToken);

        await using var stream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, cancellationToken);
        var headerBlock = Http3PreviewCodec.EncodeHeaderBlock(
        [
            new Http1HeaderField(":method", method),
            new Http1HeaderField(":scheme", "https"),
            new Http1HeaderField(":authority", "localhost"),
            new Http1HeaderField(":path", target)
        ]);
        using var request = new MemoryStream();
        Http3PreviewCodec.WriteFrame(request, Http3PreviewCodec.HeadersFrame, headerBlock);
        await stream.WriteAsync(request.ToArray(), completeWrites: true, cancellationToken);

        var responseBytes = await ReadToEndAsync(stream, cancellationToken);
        var offset = 0;
        IReadOnlyList<Http1HeaderField> headers = [];
        var body = "";
        while (offset < responseBytes.Length)
        {
            if (!Http3PreviewCodec.TryReadFrame(responseBytes, ref offset, out var type, out var payload))
            {
                break;
            }

            if (type == Http3PreviewCodec.HeadersFrame)
            {
                AssertEx.True(Http3PreviewCodec.TryDecodeHeaderBlock(payload.Span, 32 * 1024, out headers, out var reason), reason);
            }
            else if (type == Http3PreviewCodec.DataFrame)
            {
                body += Encoding.UTF8.GetString(payload.Span);
            }
        }

        await connection.CloseAsync(0, CancellationToken.None);
        return new Http3Response(headers, body);
    }

    private static async Task<byte[]> ReadToEndAsync(QuicStream stream, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[4096];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return memory.ToArray();
            }

            memory.Write(buffer, 0, read);
        }
    }

    private static IHost BuildProxyHost(
        string dataDirectory,
        Action<IServiceCollection>? configureServices = null)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(builder =>
            {
                builder.Sources.Clear();
                builder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"{MdravaDataDirectoryOptions.SectionName}:DataDirectory"] = dataDirectory
                });
            })
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureServices((context, services) =>
            {
                services.AddProxyDataPlane(context.Configuration);
                configureServices?.Invoke(services);
            })
            .Build();
    }

    private static RuntimeListener PreviewListener(string protocols, bool experimental)
    {
        return new RuntimeListener(
            "main",
            "127.0.0.1",
            8443,
            true,
            RuntimeListenerTransport.Https,
            "default",
            [],
            512,
            32 * 1024,
            32 * 1024,
            1024,
            64 * 1024)
        {
            Protocols = protocols.ToLowerInvariant() switch
            {
                "http1andhttp2andhttp3preview" => RuntimeListenerProtocols.Http1AndHttp2AndHttp3Preview,
                "http1andhttp3preview" => RuntimeListenerProtocols.Http1AndHttp3Preview,
                _ => RuntimeListenerProtocols.Http3Preview
            },
            ExperimentalHttp3 = experimental
        };
    }

    private static void WriteCertificateConfig(string dataDirectory)
    {
        var certificatePath = Path.Combine(dataDirectory, "certs", "home.pfx");
        TestCertificates.WriteSelfSignedPfx(certificatePath, "localhost", "secret");
        ConfigurationTests.WriteOperationalConfig(
            dataDirectory,
            certificateId: "home-cert",
            certificatePath: "certs/home.pfx",
            certificatePassword: "secret");
    }

    private static void WriteHttp3Site(
        string dataDirectory,
        int port,
        string protocols,
        string staticBody,
        bool experimental = true)
    {
        var sites = Directory.CreateDirectory(Path.Combine(dataDirectory, "config", "sites")).FullName;
        File.WriteAllText(
            Path.Combine(sites, "http3.json"),
            $$"""
            {
              "name": "http3",
              "listeners": [
                {
                  "name": "main",
                  "address": "127.0.0.1",
                  "port": {{port}},
                  "transport": "https",
                  "protocols": "{{protocols}}",
                  "experimentalHttp3": {{experimental.ToString().ToLowerInvariant()}},
                  "defaultCertificateId": "home-cert"
                }
              ],
              "host": "localhost",
              "routes": [
                {
                  "name": "static",
                  "pathPrefix": "/",
                  "action": "staticResponse",
                  "staticResponse": {
                    "statusCode": 200,
                    "contentType": "text/plain; charset=utf-8",
                    "body": "{{staticBody}}"
                  }
                }
              ]
            }
            """);
    }

    private static async Task<ProxyListenerStatus> WaitForListenerAsync(
        ProxyRuntimeState runtimeState,
        string name,
        string kind,
        ProxyListenerState state,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var listener = runtimeState.Snapshot().Listeners.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(candidate.Kind, kind, StringComparison.OrdinalIgnoreCase)
                && candidate.State == state);
            if (listener is not null)
            {
                return listener;
            }

            await Task.Delay(25, cancellationToken);
        }
    }

    private static async Task WaitForNoListenerAsync(
        ProxyRuntimeState runtimeState,
        string name,
        string kind,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            if (!runtimeState.Snapshot().Listeners.Any(candidate =>
                    string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(candidate.Kind, kind, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            await Task.Delay(25, cancellationToken);
        }
    }

    private static int GetFreeTcpUdpPort()
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Socket? udp = null;
            try
            {
                udp = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                udp.Bind(new IPEndPoint(IPAddress.Loopback, port));
                return port;
            }
            catch (SocketException)
            {
            }
            finally
            {
                udp?.Dispose();
                listener.Stop();
            }
        }

        throw new InvalidOperationException("Could not find a free TCP/UDP port pair.");
    }

    private static string HeaderValue(IReadOnlyList<Http1HeaderField> headers, string name)
    {
        return headers.First(header => string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private sealed class FailingQuicListenerFactory : IHttp3QuicListenerFactory
    {
        public bool IsSupported => true;

        public ValueTask<QuicListener> ListenAsync(
            RuntimeListener listener,
            ProxyConfigurationSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            _ = listener;
            _ = snapshot;
            _ = cancellationToken;
            throw new InvalidOperationException("fake_quic_bind_failure");
        }
    }

    private sealed record Http3Response(
        IReadOnlyList<Http1HeaderField> Headers,
        string Body);

    private sealed class Http3ScenarioResult : IDisposable
    {
        private readonly TemporaryDirectory _directory;
        private readonly IHost _host;

        public Http3ScenarioResult(
            TemporaryDirectory directory,
            IHost host,
            IReadOnlyList<Http1HeaderField> headers,
            string body,
            ProxyMetricsSnapshot metrics)
        {
            _directory = directory;
            _host = host;
            Headers = headers;
            Body = body;
            Metrics = metrics;
        }

        public IReadOnlyList<Http1HeaderField> Headers { get; }

        public string Body { get; }

        public ProxyMetricsSnapshot Metrics { get; }

        public void Dispose()
        {
            try
            {
                _host.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
                _host.Dispose();
            }
            finally
            {
                _directory.Dispose();
            }
        }
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
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mdrava-h3-{Guid.NewGuid():N}");
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
