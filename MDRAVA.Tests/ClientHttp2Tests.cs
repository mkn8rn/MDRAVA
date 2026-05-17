using System.Buffers.Binary;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using MDRAVA.API.Controllers;
using MDRAVA.API.Proxy.Configuration;
using MDRAVA.API.Proxy.Hosting;
using MDRAVA.API.Proxy.Metrics;
using MDRAVA.API.Proxy.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MDRAVA.Tests;

internal static class ClientHttp2Tests
{
    public static async Task ExistingHttp1BehaviorRemainsUnchanged()
    {
        var proxyPort = GetFreeTcpPort();
        var upstreamPort = GetFreeTcpPort();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var dataDirectory = CreateDataDirectory();

        try
        {
            WriteHttpsProxySite(dataDirectory, proxyPort, upstreamPort, listenerProtocols: "http1AndHttp2");
            WriteCertificateConfig(dataDirectory);
            var upstreamTask = RunSingleResponseUpstreamAsync(
                upstreamPort,
                "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 7\r\n\r\nproxied",
                timeout.Token);
            using var host = BuildProxyHost(dataDirectory);
            await host.StartAsync(timeout.Token);

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, proxyPort, timeout.Token);
                await using var tls = new SslStream(client.GetStream(), false, (_, _, _, _) => true);
                await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = "home.test",
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    ApplicationProtocols = [SslApplicationProtocol.Http11]
                }, timeout.Token);

                AssertEx.Equal(SslApplicationProtocol.Http11, tls.NegotiatedApplicationProtocol);
                await tls.WriteAsync(
                    Encoding.ASCII.GetBytes("GET /http1 HTTP/1.1\r\nHost: home.test\r\nConnection: close\r\n\r\n"),
                    timeout.Token);
                var response = await ReadToEndAsync(tls, timeout.Token);
                var upstreamRequest = await upstreamTask.WaitAsync(timeout.Token);

                AssertEx.True(response.Contains("200 OK", StringComparison.Ordinal), response);
                AssertEx.True(upstreamRequest.StartsWith("GET /http1 HTTP/1.1", StringComparison.Ordinal), upstreamRequest);
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

    public static void PlaintextHttp2ListenerIsRejected()
    {
        var validation = new ProxyOptionsValidator().Validate(
            null,
            new ProxyOptions
            {
                Listeners =
                [
                    new ListenerOptions
                    {
                        Name = "bad-h2c",
                        Address = "127.0.0.1",
                        Port = 8080,
                        Transport = "http",
                        Protocols = "http2"
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
                                Name = "local",
                                Address = "127.0.0.1",
                                Port = 5000
                            }
                        ]
                    }
                ]
            });

        AssertEx.True(validation.Failed);
        AssertEx.True(
            AssertEx.NotNull(validation.Failures)
                .Any(static failure => failure.Contains("HTTP/2 requires an HTTPS listener", StringComparison.Ordinal)));
    }

    public static async Task AlpnSelectsHttp2WhenEnabled()
    {
        var result = await RunHttp2ScenarioAsync(
            SiteWithStaticRoute,
            request => request.Authority = "home.test");

        AssertEx.Equal(SslApplicationProtocol.Http2, result.NegotiatedProtocol);
        AssertEx.Equal(203, result.Response.StatusCode);
    }

    public static async Task Http2RequestMapsToRouteMatcher()
    {
        var result = await RunHttp2ScenarioAsync(
            (dataDirectory, proxyPort, upstreamPort) =>
                WriteHttpsProxySite(dataDirectory, proxyPort, upstreamPort),
            request =>
            {
                request.Path = "/h2";
                request.Authority = "home.test";
            },
            upstreamResponse: "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 7\r\n\r\nproxied");

        AssertEx.Equal(200, result.Response.StatusCode);
        AssertEx.Equal("proxied", result.Response.BodyText);
        AssertEx.True(result.UpstreamRequest.StartsWith("GET /h2 HTTP/1.1", StringComparison.Ordinal), result.UpstreamRequest);
    }

    public static async Task AuthorityMapsToHostRouting()
    {
        var result = await RunHttp2ScenarioAsync(
            (dataDirectory, proxyPort, upstreamPort) =>
                WriteHttpsProxySite(dataDirectory, proxyPort, upstreamPort, host: "authority.test"),
            request =>
            {
                request.Authority = "authority.test";
                request.Path = "/authority";
            },
            upstreamResponse: "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 2\r\n\r\nok");

        AssertEx.Equal(200, result.Response.StatusCode);
        AssertEx.True(result.UpstreamRequest.Contains("Host: authority.test", StringComparison.OrdinalIgnoreCase), result.UpstreamRequest);
    }

    public static async Task QueryStringIsPreserved()
    {
        var result = await RunHttp2ScenarioAsync(
            (dataDirectory, proxyPort, upstreamPort) =>
                WriteHttpsProxySite(dataDirectory, proxyPort, upstreamPort),
            request =>
            {
                request.Path = "/search?q=one&sort=two";
                request.Authority = "home.test";
            },
            upstreamResponse: "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 2\r\n\r\nok");

        AssertEx.True(
            result.UpstreamRequest.StartsWith("GET /search?q=one&sort=two HTTP/1.1", StringComparison.Ordinal),
            result.UpstreamRequest);
    }

    public static async Task InvalidPseudoHeadersAreRejected()
    {
        var result = await RunHttp2ScenarioAsync(
            SiteWithStaticRoute,
            request =>
            {
                request.Authority = "home.test";
                request.DuplicatePathPseudoHeader = true;
            });

        AssertEx.Equal(400, result.Response.StatusCode);
        AssertEx.Equal(1L, result.Metrics.Http2ProtocolErrors["invalid_pseudo_header"]);
    }

    public static async Task ForbiddenConnectionHeadersAreRejected()
    {
        var result = await RunHttp2ScenarioAsync(
            SiteWithStaticRoute,
            request =>
            {
                request.Authority = "home.test";
                request.Headers.Add(("connection", "close"));
            });

        AssertEx.Equal(400, result.Response.StatusCode);
        AssertEx.Equal(1L, result.Metrics.Http2ProtocolErrors["forbidden_header"]);
    }

    public static async Task HuffmanRequestHeaderValuesAreDecoded()
    {
        var result = await RunHttp2ScenarioAsync(
            (dataDirectory, proxyPort, upstreamPort) =>
                WriteHttpsProxySite(dataDirectory, proxyPort, upstreamPort),
            request =>
            {
                request.Authority = "home.test";
                request.Path = "/huffman";
                request.HuffmanValueHeaders.Add(("x-huffman-hpack", "mdrava"));
            },
            upstreamResponse: "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 2\r\n\r\nok");

        AssertEx.Equal(200, result.Response.StatusCode);
        AssertEx.True(result.UpstreamRequest.Contains("x-huffman-hpack: mdrava", StringComparison.OrdinalIgnoreCase), result.UpstreamRequest);
    }

    public static async Task ResponseOmitsHopByHopHeaders()
    {
        var result = await RunHttp2ScenarioAsync(
            (dataDirectory, proxyPort, upstreamPort) =>
                WriteHttpsProxySite(dataDirectory, proxyPort, upstreamPort),
            request =>
            {
                request.Authority = "home.test";
                request.Path = "/headers";
            },
            upstreamResponse: "HTTP/1.1 200 OK\r\nConnection: close\r\nKeep-Alive: timeout=5\r\nContent-Length: 2\r\n\r\nok");

        AssertEx.Equal(200, result.Response.StatusCode);
        AssertEx.False(result.Response.Headers.ContainsKey("connection"));
        AssertEx.False(result.Response.Headers.ContainsKey("keep-alive"));
    }

    public static async Task StaticResponseRouteWorksOverHttp2()
    {
        var result = await RunHttp2ScenarioAsync(
            SiteWithStaticRoute,
            request =>
            {
                request.Authority = "home.test";
                request.Path = "/static";
            });

        AssertEx.Equal(203, result.Response.StatusCode);
        AssertEx.Equal("static-h2", result.Response.BodyText);
    }

    public static async Task RedirectRouteWorksOverHttp2()
    {
        var result = await RunHttp2ScenarioAsync(
            SiteWithRedirectRoute,
            request =>
            {
                request.Authority = "home.test";
                request.Path = "/old?id=1";
            });

        AssertEx.Equal(308, result.Response.StatusCode);
        AssertEx.Equal("/new?id=1", result.Response.Header("location"));
    }

    public static async Task MaintenanceRouteWorksOverHttp2()
    {
        var result = await RunHttp2ScenarioAsync(
            SiteWithMaintenanceRoute,
            request =>
            {
                request.Authority = "home.test";
                request.Path = "/maintenance";
            });

        AssertEx.Equal(503, result.Response.StatusCode);
        AssertEx.Equal("maintenance", result.Response.BodyText);
    }

    public static async Task HeadReturnsHeadersWithoutBody()
    {
        var result = await RunHttp2ScenarioAsync(
            (dataDirectory, proxyPort, upstreamPort) =>
                WriteHttpsProxySite(dataDirectory, proxyPort, upstreamPort),
            request =>
            {
                request.Method = "HEAD";
                request.Authority = "home.test";
                request.Path = "/head";
            },
            upstreamResponse: "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 5\r\nX-Head: yes\r\n\r\nhello");

        AssertEx.Equal(200, result.Response.StatusCode);
        AssertEx.Equal("yes", result.Response.Header("x-head"));
        AssertEx.Equal("", result.Response.BodyText);
    }

    public static async Task CacheWorksOverHttp2()
    {
        var proxyPort = GetFreeTcpPort();
        var upstreamPort = GetFreeTcpPort();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var dataDirectory = CreateDataDirectory();

        try
        {
            WriteHttpsProxySite(
                dataDirectory,
                proxyPort,
                upstreamPort,
                routeExtraJson:
                """
                  "cache": {
                    "enabled": true,
                    "maxEntryBytes": 4096,
                    "maxTotalBytes": 8192,
                    "defaultTtlSeconds": 60,
                    "respectOriginCacheControl": true
                  },
                """);
            WriteCertificateConfig(dataDirectory);
            var upstreamTask = RunSingleResponseUpstreamAsync(
                upstreamPort,
                "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 9\r\nCache-Control: max-age=60\r\n\r\ncache-hit",
                timeout.Token);
            using var host = BuildProxyHost(dataDirectory);
            await host.StartAsync(timeout.Token);

            try
            {
                await using var client = await Http2TestClient.ConnectAsync(proxyPort, timeout.Token);
                var first = await client.SendRequestAsync(new Http2RequestSpec
                {
                    Authority = "home.test",
                    Path = "/cache"
                }, timeout.Token);
                var second = await client.SendRequestAsync(new Http2RequestSpec
                {
                    Authority = "home.test",
                    Path = "/cache"
                }, timeout.Token);
                var upstreamRequest = await upstreamTask.WaitAsync(timeout.Token);
                var metrics = host.Services.GetRequiredService<ProxyMetrics>().Snapshot();

                AssertEx.Equal(200, first.StatusCode);
                AssertEx.Equal(200, second.StatusCode);
                AssertEx.Equal("cache-hit", second.BodyText);
                AssertEx.True(second.Headers.ContainsKey("age"));
                AssertEx.True(upstreamRequest.StartsWith("GET /cache HTTP/1.1", StringComparison.Ordinal), upstreamRequest);
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

    public static async Task RetryWorksForHttp2ProxyRequests()
    {
        var proxyPort = GetFreeTcpPort();
        var firstUpstreamPort = GetFreeTcpPort();
        var secondUpstreamPort = GetFreeTcpPort();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var dataDirectory = CreateDataDirectory();

        try
        {
            WriteHttpsRetrySite(dataDirectory, proxyPort, firstUpstreamPort, secondUpstreamPort);
            WriteCertificateConfig(dataDirectory);
            ConfigurationTests.WriteOperationalConfig(
                dataDirectory,
                upstreamConnectTimeoutMs: 150,
                upstreamResponseHeadTimeoutMs: 500,
                certificateId: "home-cert",
                certificatePath: "certs/home.pfx");
            var upstreamTask = RunSingleResponseUpstreamAsync(
                secondUpstreamPort,
                "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 7\r\n\r\nretried",
                timeout.Token);
            using var host = BuildProxyHost(dataDirectory);
            await host.StartAsync(timeout.Token);

            try
            {
                await using var client = await Http2TestClient.ConnectAsync(proxyPort, timeout.Token);
                var response = await client.SendRequestAsync(new Http2RequestSpec
                {
                    Authority = "home.test",
                    Path = "/retry"
                }, timeout.Token);
                var upstreamRequest = await upstreamTask.WaitAsync(timeout.Token);
                var metrics = host.Services.GetRequiredService<ProxyMetrics>().Snapshot();

                AssertEx.Equal(200, response.StatusCode);
                AssertEx.Equal("retried", response.BodyText);
                AssertEx.True(upstreamRequest.StartsWith("GET /retry HTTP/1.1", StringComparison.Ordinal), upstreamRequest);
                AssertEx.True(metrics.RetryAttempts >= 1, metrics.RetryAttempts.ToString());
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

    public static async Task ExtendedConnectIsRejected()
    {
        var result = await RunHttp2ScenarioAsync(
            SiteWithStaticRoute,
            request =>
            {
                request.Method = "CONNECT";
                request.Authority = "home.test";
                request.Path = "/socket";
                request.Headers.Add((":protocol", "websocket"));
            });

        AssertEx.Equal(400, result.Response.StatusCode);
        AssertEx.True(result.Metrics.Http2ProtocolErrors.ContainsKey("invalid_pseudo_header")
            || result.Metrics.Http2ProtocolErrors.ContainsKey("extended_connect_unsupported"));
    }

    public static async Task MetricsIncludeHttp2Counters()
    {
        var result = await RunHttp2ScenarioAsync(
            SiteWithStaticRoute,
            request =>
            {
                request.Authority = "home.test";
                request.Path = "/metrics";
            });

        AssertEx.True(result.Metrics.Http2AcceptedConnections >= 1, result.Metrics.Http2AcceptedConnections.ToString());
        AssertEx.True(result.Metrics.Http2Requests >= 1, result.Metrics.Http2Requests.ToString());
        AssertEx.Equal(0L, result.Metrics.ActiveHttp2Streams);
    }

    private static async Task<Http2ScenarioResult> RunHttp2ScenarioAsync(
        Action<string, int, int> writeSite,
        Action<Http2RequestSpec> configureRequest,
        string upstreamResponse = "",
        bool expectUpstream = false)
    {
        var proxyPort = GetFreeTcpPort();
        var upstreamPort = GetFreeTcpPort();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var dataDirectory = CreateDataDirectory();

        try
        {
            writeSite(dataDirectory, proxyPort, upstreamPort);
            WriteCertificateConfig(dataDirectory);
            var upstreamTask = expectUpstream || !string.IsNullOrEmpty(upstreamResponse)
                ? RunSingleResponseUpstreamAsync(upstreamPort, upstreamResponse, timeout.Token)
                : Task.FromResult("");
            using var host = BuildProxyHost(dataDirectory);
            await host.StartAsync(timeout.Token);

            try
            {
                await using var client = await Http2TestClient.ConnectAsync(proxyPort, timeout.Token);
                var request = new Http2RequestSpec();
                configureRequest(request);
                var response = await client.SendRequestAsync(request, timeout.Token);
                var upstreamRequest = await upstreamTask.WaitAsync(timeout.Token);
                var metrics = host.Services.GetRequiredService<ProxyMetrics>().Snapshot();
                var diagnostics = host.Services.GetRequiredService<RecentRequestDiagnosticsStore>().Recent(50);
                return new Http2ScenarioResult(
                    response,
                    upstreamRequest,
                    metrics,
                    diagnostics,
                    client.NegotiatedProtocol);
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

    private static void WriteHttpsProxySite(
        string dataDirectory,
        int proxyPort,
        int upstreamPort,
        string host = "*",
        string listenerProtocols = "http2",
        string routeExtraJson = "")
    {
        ConfigurationTests.WriteCustomSite(
            dataDirectory,
            "h2.json",
            $$"""
            {
              "name": "h2",
              "listeners": [
                {
                  "name": "main",
                  "address": "127.0.0.1",
                  "port": {{proxyPort}},
                  "transport": "https",
                  "protocols": "{{listenerProtocols}}",
                  "defaultCertificateId": "home-cert",
                  "http2MaxConcurrentStreams": 32,
                  "http2MaxHeaderListBytes": 32768,
                  "http2MaxFrameSize": 16384,
                  "sniCertificates": [
                    {
                      "hostName": "home.test",
                      "certificateId": "home-cert"
                    }
                  ]
                }
              ],
              "host": "{{host}}",
              "routes": [
                {
                  "name": "h2-proxy",
                  "pathPrefix": "/",
                  "action": "proxy",
                  {{routeExtraJson}}
                  "upstreams": [
                    {
                      "name": "local-test",
                      "address": "127.0.0.1",
                      "port": {{upstreamPort}}
                    }
                  ]
                }
              ]
            }
            """);
    }

    private static void SiteWithStaticRoute(string dataDirectory, int proxyPort, int upstreamPort)
    {
        _ = upstreamPort;
        ConfigurationTests.WriteCustomSite(
            dataDirectory,
            "h2.json",
            $$"""
            {
              "name": "h2",
              "listeners": [
                {
                  "name": "main",
                  "address": "127.0.0.1",
                  "port": {{proxyPort}},
                  "transport": "https",
                  "protocols": "http2",
                  "defaultCertificateId": "home-cert",
                  "sniCertificates": [
                    {
                      "hostName": "home.test",
                      "certificateId": "home-cert"
                    }
                  ]
                }
              ],
              "host": "*",
              "routes": [
                {
                  "name": "static",
                  "pathPrefix": "/",
                  "action": "staticResponse",
                  "staticResponse": {
                    "statusCode": 203,
                    "contentType": "text/plain",
                    "body": "static-h2"
                  }
                }
              ]
            }
            """);
    }

    private static void SiteWithRedirectRoute(string dataDirectory, int proxyPort, int upstreamPort)
    {
        _ = upstreamPort;
        ConfigurationTests.WriteCustomSite(
            dataDirectory,
            "h2.json",
            $$"""
            {
              "name": "h2",
              "listeners": [
                {
                  "name": "main",
                  "address": "127.0.0.1",
                  "port": {{proxyPort}},
                  "transport": "https",
                  "protocols": "http2",
                  "defaultCertificateId": "home-cert",
                  "sniCertificates": [
                    {
                      "hostName": "home.test",
                      "certificateId": "home-cert"
                    }
                  ]
                }
              ],
              "host": "*",
              "routes": [
                {
                  "name": "redirect",
                  "pathPrefix": "/old",
                  "action": "redirect",
                  "redirect": {
                    "statusCode": 308,
                    "targetPath": "/new",
                    "preserveQuery": true
                  }
                }
              ]
            }
            """);
    }

    private static void SiteWithMaintenanceRoute(string dataDirectory, int proxyPort, int upstreamPort)
    {
        _ = upstreamPort;
        ConfigurationTests.WriteCustomSite(
            dataDirectory,
            "h2.json",
            $$"""
            {
              "name": "h2",
              "listeners": [
                {
                  "name": "main",
                  "address": "127.0.0.1",
                  "port": {{proxyPort}},
                  "transport": "https",
                  "protocols": "http2",
                  "defaultCertificateId": "home-cert",
                  "sniCertificates": [
                    {
                      "hostName": "home.test",
                      "certificateId": "home-cert"
                    }
                  ]
                }
              ],
              "host": "*",
              "routes": [
                {
                  "name": "maintenance",
                  "pathPrefix": "/maintenance",
                  "action": "proxy",
                  "maintenance": {
                    "enabled": true,
                    "body": "maintenance"
                  },
                  "upstreams": [
                    {
                      "name": "unused",
                      "address": "127.0.0.1",
                      "port": 1
                    }
                  ]
                }
              ]
            }
            """);
    }

    private static void WriteHttpsRetrySite(
        string dataDirectory,
        int proxyPort,
        int firstUpstreamPort,
        int secondUpstreamPort)
    {
        ConfigurationTests.WriteCustomSite(
            dataDirectory,
            "h2.json",
            $$"""
            {
              "name": "h2",
              "listeners": [
                {
                  "name": "main",
                  "address": "127.0.0.1",
                  "port": {{proxyPort}},
                  "transport": "https",
                  "protocols": "http2",
                  "defaultCertificateId": "home-cert",
                  "sniCertificates": [
                    {
                      "hostName": "home.test",
                      "certificateId": "home-cert"
                    }
                  ]
                }
              ],
              "host": "*",
              "routes": [
                {
                  "name": "retry",
                  "pathPrefix": "/",
                  "action": "proxy",
                  "retry": {
                    "enabled": true,
                    "maxAttempts": 2,
                    "retryOnConnectFailure": true,
                    "retryMethods": [ "GET", "HEAD" ],
                    "retryBackoffMilliseconds": 0
                  },
                  "upstreams": [
                    {
                      "name": "down",
                      "address": "127.0.0.1",
                      "port": {{firstUpstreamPort}}
                    },
                    {
                      "name": "up",
                      "address": "127.0.0.1",
                      "port": {{secondUpstreamPort}}
                    }
                  ]
                }
              ]
            }
            """);
    }

    private static void WriteCertificateConfig(string dataDirectory)
    {
        TestCertificates.WriteSelfSignedPfx(Path.Combine(dataDirectory, "certs", "home.pfx"), "home.test");
        ConfigurationTests.WriteOperationalConfig(
            dataDirectory,
            certificateId: "home-cert",
            certificatePath: "certs/home.pfx");
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

    private static async Task<string> RunSingleResponseUpstreamAsync(
        int upstreamPort,
        string response,
        CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Loopback, upstreamPort);
        listener.Start();

        try
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken);
            await using var stream = client.GetStream();
            var request = await ReadRequestAsync(stream, cancellationToken);
            if (!string.IsNullOrEmpty(response))
            {
                await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken);
            }

            return request;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task<string> ReadRequestAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var bytes = new MemoryStream();
        var headText = "";
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            bytes.Write(buffer, 0, read);
            var data = bytes.ToArray();
            var headEnd = IndexOfHeaderEnd(data);
            if (headEnd < 0)
            {
                continue;
            }

            headText = Encoding.ASCII.GetString(data, 0, headEnd);
            var contentLength = ContentLength(headText);
            var bodyBytes = data.Length - headEnd - 4;
            while (bodyBytes < contentLength)
            {
                read = await stream.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                bytes.Write(buffer, 0, read);
                bodyBytes += read;
            }

            break;
        }

        return Encoding.ASCII.GetString(bytes.ToArray());
    }

    private static int ContentLength(string headText)
    {
        foreach (var line in headText.Split("\r\n", StringSplitOptions.None))
        {
            if (!line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return int.TryParse(line["Content-Length:".Length..].Trim(), out var value) ? value : 0;
        }

        return 0;
    }

    private static int IndexOfHeaderEnd(ReadOnlySpan<byte> bytes)
    {
        for (var index = 3; index < bytes.Length; index++)
        {
            if (bytes[index - 3] == (byte)'\r'
                && bytes[index - 2] == (byte)'\n'
                && bytes[index - 1] == (byte)'\r'
                && bytes[index] == (byte)'\n')
            {
                return index - 3;
            }
        }

        return -1;
    }

    private static async Task<string> ReadToEndAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var builder = new StringBuilder();
        while (true)
        {
            int read;
            try
            {
                read = await stream.ReadAsync(buffer, cancellationToken);
            }
            catch (IOException)
            {
                break;
            }

            if (read == 0)
            {
                break;
            }

            builder.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }

        return builder.ToString();
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

    private static string CreateDataDirectory()
    {
        return Path.Combine(Path.GetTempPath(), $"mdrava-h2-{Guid.NewGuid():N}");
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private sealed class Http2RequestSpec
    {
        public string Method { get; set; } = "GET";

        public string Scheme { get; set; } = "https";

        public string Authority { get; set; } = "home.test";

        public string Path { get; set; } = "/";

        public List<(string Name, string Value)> Headers { get; } = [];

        public List<(string Name, string Value)> HuffmanValueHeaders { get; } = [];

        public byte[] Body { get; set; } = [];

        public bool DuplicatePathPseudoHeader { get; set; }
    }

    private sealed record Http2ScenarioResult(
        Http2Response Response,
        string UpstreamRequest,
        ProxyMetricsSnapshot Metrics,
        IReadOnlyList<ProxyRequestDiagnosticEvent> Diagnostics,
        SslApplicationProtocol NegotiatedProtocol);

    private sealed class Http2Response
    {
        public Http2Response(int statusCode, IReadOnlyDictionary<string, string> headers, byte[] body)
        {
            StatusCode = statusCode;
            Headers = headers;
            Body = body;
        }

        public int StatusCode { get; }

        public IReadOnlyDictionary<string, string> Headers { get; }

        public byte[] Body { get; }

        public string BodyText => Encoding.UTF8.GetString(Body);

        public string Header(string name)
        {
            return Headers.TryGetValue(name, out var value) ? value : "";
        }
    }

    private sealed class Http2TestClient : IAsyncDisposable
    {
        private static readonly byte[] ClientPreface = "PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"u8.ToArray();
        private readonly TcpClient _client;
        private readonly SslStream _stream;
        private int _nextStreamId = 1;

        private Http2TestClient(TcpClient client, SslStream stream)
        {
            _client = client;
            _stream = stream;
        }

        public SslApplicationProtocol NegotiatedProtocol => _stream.NegotiatedApplicationProtocol;

        public static async Task<Http2TestClient> ConnectAsync(int port, CancellationToken cancellationToken)
        {
            var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port, cancellationToken);
            var stream = new SslStream(client.GetStream(), false, (_, _, _, _) => true);
            await stream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = "home.test",
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                ApplicationProtocols = [SslApplicationProtocol.Http2]
            }, cancellationToken);
            var http2 = new Http2TestClient(client, stream);
            await http2.InitializeAsync(cancellationToken);
            return http2;
        }

        public async Task<Http2Response> SendRequestAsync(
            Http2RequestSpec request,
            CancellationToken cancellationToken)
        {
            var streamId = _nextStreamId;
            _nextStreamId += 2;
            var headers = EncodeRequestHeaders(request);
            await WriteFrameAsync(
                Http2FrameType.Headers,
                request.Body.Length == 0 ? (byte)(Http2Flags.EndHeaders | Http2Flags.EndStream) : Http2Flags.EndHeaders,
                streamId,
                headers,
                cancellationToken);
            if (request.Body.Length > 0)
            {
                await WriteFrameAsync(Http2FrameType.Data, Http2Flags.EndStream, streamId, request.Body, cancellationToken);
            }

            return await ReadResponseAsync(streamId, cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _stream.DisposeAsync();
            _client.Dispose();
        }

        private async Task InitializeAsync(CancellationToken cancellationToken)
        {
            await _stream.WriteAsync(ClientPreface, cancellationToken);
            await WriteFrameAsync(Http2FrameType.Settings, 0, 0, ReadOnlyMemory<byte>.Empty, cancellationToken);

            while (true)
            {
                var frame = await ReadFrameAsync(cancellationToken);
                if (frame.Type != Http2FrameType.Settings)
                {
                    continue;
                }

                if ((frame.Flags & Http2Flags.Ack) == 0)
                {
                    await WriteFrameAsync(Http2FrameType.Settings, Http2Flags.Ack, 0, ReadOnlyMemory<byte>.Empty, cancellationToken);
                    return;
                }
            }
        }

        private async Task<Http2Response> ReadResponseAsync(int streamId, CancellationToken cancellationToken)
        {
            var headerBlock = new MemoryStream();
            var body = new MemoryStream();
            Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
            var statusCode = 0;

            while (true)
            {
                var frame = await ReadFrameAsync(cancellationToken);
                if (frame.Type == Http2FrameType.Settings)
                {
                    if ((frame.Flags & Http2Flags.Ack) == 0)
                    {
                        await WriteFrameAsync(Http2FrameType.Settings, Http2Flags.Ack, 0, ReadOnlyMemory<byte>.Empty, cancellationToken);
                    }

                    continue;
                }

                if (frame.StreamId != streamId)
                {
                    continue;
                }

                if (frame.Type == Http2FrameType.Headers || frame.Type == Http2FrameType.Continuation)
                {
                    headerBlock.Write(frame.Payload.Span);
                    if ((frame.Flags & Http2Flags.EndHeaders) != 0)
                    {
                        foreach (var header in DecodeHeaders(headerBlock.ToArray()))
                        {
                            if (string.Equals(header.Name, ":status", StringComparison.Ordinal))
                            {
                                statusCode = int.Parse(header.Value);
                            }
                            else
                            {
                                headers[header.Name] = header.Value;
                            }
                        }
                    }

                    if ((frame.Flags & Http2Flags.EndStream) != 0)
                    {
                        return new Http2Response(statusCode, headers, body.ToArray());
                    }
                }
                else if (frame.Type == Http2FrameType.Data)
                {
                    body.Write(frame.Payload.Span);
                    if ((frame.Flags & Http2Flags.EndStream) != 0)
                    {
                        return new Http2Response(statusCode, headers, body.ToArray());
                    }
                }
                else if (frame.Type == Http2FrameType.RstStream)
                {
                    return new Http2Response(0, headers, body.ToArray());
                }
            }
        }

        private async Task<Http2Frame> ReadFrameAsync(CancellationToken cancellationToken)
        {
            var header = await ReadExactAsync(9, cancellationToken);
            var length = header[0] << 16 | header[1] << 8 | header[2];
            var payload = length == 0 ? [] : await ReadExactAsync(length, cancellationToken);
            return new Http2Frame(
                (Http2FrameType)header[3],
                header[4],
                (int)(BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(5, 4)) & 0x7fffffff),
                payload);
        }

        private async Task<byte[]> ReadExactAsync(int length, CancellationToken cancellationToken)
        {
            var buffer = new byte[length];
            var offset = 0;
            while (offset < length)
            {
                var read = await _stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken);
                if (read == 0)
                {
                    throw new IOException("HTTP/2 connection closed while reading a frame.");
                }

                offset += read;
            }

            return buffer;
        }

        private async Task WriteFrameAsync(
            Http2FrameType type,
            byte flags,
            int streamId,
            ReadOnlyMemory<byte> payload,
            CancellationToken cancellationToken)
        {
            var header = new byte[9];
            header[0] = (byte)((payload.Length >> 16) & 0xff);
            header[1] = (byte)((payload.Length >> 8) & 0xff);
            header[2] = (byte)(payload.Length & 0xff);
            header[3] = (byte)type;
            header[4] = flags;
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(5, 4), (uint)streamId & 0x7fffffff);
            await _stream.WriteAsync(header, cancellationToken);
            if (payload.Length > 0)
            {
                await _stream.WriteAsync(payload, cancellationToken);
            }
        }

        private static byte[] EncodeRequestHeaders(Http2RequestSpec request)
        {
            using var memory = new MemoryStream();
            WriteMethod(memory, request.Method);
            WriteLiteralWithIndexedName(memory, 7, request.Scheme);
            WriteLiteralWithIndexedName(memory, 1, request.Authority);
            WriteLiteralWithIndexedName(memory, 4, request.Path);
            if (request.DuplicatePathPseudoHeader)
            {
                WriteLiteralWithIndexedName(memory, 4, request.Path);
            }

            foreach (var header in request.Headers)
            {
                if (header.Name.StartsWith(':'))
                {
                    WriteLiteral(memory, header.Name, header.Value);
                }
                else
                {
                    WriteLiteral(memory, header.Name, header.Value);
                }
            }

            foreach (var header in request.HuffmanValueHeaders)
            {
                WriteLiteralWithHuffmanValue(memory, header.Name, header.Value);
            }

            return memory.ToArray();
        }

        private static void WriteMethod(Stream stream, string method)
        {
            if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                stream.WriteByte(0x82);
                return;
            }

            if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
            {
                stream.WriteByte(0x83);
                return;
            }

            WriteLiteralWithIndexedName(stream, 2, method);
        }

        private static void WriteLiteralWithIndexedName(Stream stream, int nameIndex, string value)
        {
            WriteInteger(stream, 0, 4, nameIndex);
            WriteString(stream, value);
        }

        private static void WriteLiteral(Stream stream, string name, string value)
        {
            stream.WriteByte(0);
            WriteString(stream, name);
            WriteString(stream, value);
        }

        private static void WriteLiteralWithHuffmanValue(Stream stream, string name, string value)
        {
            stream.WriteByte(0);
            WriteString(stream, name);
            WriteHuffmanString(stream, value);
        }

        private static void WriteString(Stream stream, string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            WriteInteger(stream, 0, 7, bytes.Length);
            stream.Write(bytes);
        }

        private static void WriteHuffmanString(Stream stream, string value)
        {
            var bytes = EncodeHuffmanAscii(value);
            WriteInteger(stream, 0x80, 7, bytes.Length);
            stream.Write(bytes);
        }

        private static byte[] EncodeHuffmanAscii(string value)
        {
            using var memory = new MemoryStream();
            var pendingBits = 0UL;
            var pendingLength = 0;
            foreach (var character in value)
            {
                var code = HuffmanCode(character, out var codeLength);
                pendingBits = (pendingBits << codeLength) | code;
                pendingLength += codeLength;
                while (pendingLength >= 8)
                {
                    var shift = pendingLength - 8;
                    memory.WriteByte((byte)(pendingBits >> shift));
                    pendingBits &= shift == 0 ? 0 : (1UL << shift) - 1;
                    pendingLength -= 8;
                }
            }

            if (pendingLength > 0)
            {
                pendingBits <<= 8 - pendingLength;
                pendingBits |= (1UL << (8 - pendingLength)) - 1;
                memory.WriteByte((byte)pendingBits);
            }

            return memory.ToArray();
        }

        private static ulong HuffmanCode(char character, out int length)
        {
            (ulong Code, int Length) value = character switch
            {
                'a' => (0x3, 5),
                'd' => (0x24, 6),
                'm' => (0x29, 6),
                'r' => (0x2c, 6),
                'v' => (0x77, 7),
                _ => throw new InvalidOperationException($"No test Huffman code for '{character}'.")
            };
            length = value.Length;
            return value.Code;
        }

        private static void WriteInteger(Stream stream, byte prefix, int prefixBits, int value)
        {
            var maxPrefix = (1 << prefixBits) - 1;
            if (value < maxPrefix)
            {
                stream.WriteByte((byte)(prefix | value));
                return;
            }

            stream.WriteByte((byte)(prefix | maxPrefix));
            value -= maxPrefix;
            while (value >= 128)
            {
                stream.WriteByte((byte)(value % 128 + 128));
                value /= 128;
            }

            stream.WriteByte((byte)value);
        }

        private static IReadOnlyList<(string Name, string Value)> DecodeHeaders(byte[] block)
        {
            List<(string Name, string Value)> headers = [];
            List<(string Name, string Value)> dynamicTable = [];
            var offset = 0;
            while (offset < block.Length)
            {
                var current = block[offset];
                if ((current & 0x80) != 0)
                {
                    var index = DecodeInteger(block, 7, ref offset);
                    headers.Add(GetHeader(index, dynamicTable));
                    continue;
                }

                if ((current & 0x40) != 0)
                {
                    var literal = DecodeLiteral(block, 6, ref offset, dynamicTable);
                    dynamicTable.Insert(0, literal);
                    headers.Add(literal);
                    continue;
                }

                if ((current & 0x20) != 0)
                {
                    _ = DecodeInteger(block, 5, ref offset);
                    continue;
                }

                headers.Add(DecodeLiteral(block, 4, ref offset, dynamicTable));
            }

            return headers;
        }

        private static (string Name, string Value) DecodeLiteral(
            byte[] block,
            int prefixBits,
            ref int offset,
            IReadOnlyList<(string Name, string Value)> dynamicTable)
        {
            var nameIndex = DecodeInteger(block, prefixBits, ref offset);
            var name = nameIndex == 0 ? ReadString(block, ref offset) : GetHeader(nameIndex, dynamicTable).Name;
            var value = ReadString(block, ref offset);
            return (name, value);
        }

        private static string ReadString(byte[] block, ref int offset)
        {
            if ((block[offset] & 0x80) != 0)
            {
                throw new InvalidOperationException("The test HPACK decoder does not support Huffman strings.");
            }

            var length = DecodeInteger(block, 7, ref offset);
            var value = Encoding.ASCII.GetString(block, offset, length);
            offset += length;
            return value;
        }

        private static int DecodeInteger(byte[] block, int prefixBits, ref int offset)
        {
            var mask = (1 << prefixBits) - 1;
            var value = block[offset++] & mask;
            if (value < mask)
            {
                return value;
            }

            var multiplier = 0;
            while (offset < block.Length)
            {
                var next = block[offset++];
                value += (next & 0x7f) << multiplier;
                if ((next & 0x80) == 0)
                {
                    break;
                }

                multiplier += 7;
            }

            return value;
        }

        private static (string Name, string Value) GetHeader(
            int index,
            IReadOnlyList<(string Name, string Value)> dynamicTable)
        {
            if (index > 0 && index < StaticTable.Length)
            {
                return StaticTable[index];
            }

            var dynamicIndex = index - StaticTable.Length;
            return dynamicTable[dynamicIndex];
        }

        private static readonly (string Name, string Value)[] StaticTable =
        [
            ("", ""),
            (":authority", ""),
            (":method", "GET"),
            (":method", "POST"),
            (":path", "/"),
            (":path", "/index.html"),
            (":scheme", "http"),
            (":scheme", "https"),
            (":status", "200"),
            (":status", "204"),
            (":status", "206"),
            (":status", "304"),
            (":status", "400"),
            (":status", "404"),
            (":status", "500"),
            ("accept-charset", ""),
            ("accept-encoding", "gzip, deflate"),
            ("accept-language", ""),
            ("accept-ranges", ""),
            ("accept", ""),
            ("access-control-allow-origin", ""),
            ("age", ""),
            ("allow", ""),
            ("authorization", ""),
            ("cache-control", ""),
            ("content-disposition", ""),
            ("content-encoding", ""),
            ("content-language", ""),
            ("content-length", ""),
            ("content-location", ""),
            ("content-range", ""),
            ("content-type", ""),
            ("cookie", ""),
            ("date", ""),
            ("etag", ""),
            ("expect", ""),
            ("expires", ""),
            ("from", ""),
            ("host", ""),
            ("if-match", ""),
            ("if-modified-since", ""),
            ("if-none-match", ""),
            ("if-range", ""),
            ("if-unmodified-since", ""),
            ("last-modified", ""),
            ("link", ""),
            ("location", ""),
            ("max-forwards", ""),
            ("proxy-authenticate", ""),
            ("proxy-authorization", ""),
            ("range", ""),
            ("referer", ""),
            ("refresh", ""),
            ("retry-after", ""),
            ("server", ""),
            ("set-cookie", ""),
            ("strict-transport-security", ""),
            ("transfer-encoding", ""),
            ("user-agent", ""),
            ("vary", ""),
            ("via", ""),
            ("www-authenticate", "")
        ];
    }

    private readonly record struct Http2Frame(
        Http2FrameType Type,
        byte Flags,
        int StreamId,
        ReadOnlyMemory<byte> Payload);

    private enum Http2FrameType : byte
    {
        Data = 0x0,
        Headers = 0x1,
        RstStream = 0x3,
        Settings = 0x4,
        Ping = 0x6,
        GoAway = 0x7,
        Continuation = 0x9
    }

    private static class Http2Flags
    {
        public const byte EndStream = 0x1;
        public const byte Ack = 0x1;
        public const byte EndHeaders = 0x4;
    }
}
