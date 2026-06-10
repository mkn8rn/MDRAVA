using MDRAVA.BLL.ControlPlane.Http1;
using MDRAVA.BLL.ControlPlane.HealthChecks;
using MDRAVA.BLL.ControlPlane.Timeouts;
using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane;
using MDRAVA.BLL.ControlPlane.Metrics;
using MDRAVA.BLL.ControlPlane.Upstreams;
using System.Buffers;
using System.Net.Sockets;
using System.Text;
using MDRAVA.INF.Proxy.Connections;
using MDRAVA.INF.Proxy.Forwarding;
using MDRAVA.INF.Proxy.Http2;
using MDRAVA.INF.Proxy.Http3;

namespace MDRAVA.INF.Proxy.Health;

public sealed class UpstreamHealthCheckClient : IUpstreamHealthCheckClient
{
    private const int MaxHealthResponseHeadBytes = 16 * 1024;

    private readonly UpstreamConnectionFactory _connectionFactory;
    private readonly ProxyMetrics _metrics;

    public UpstreamHealthCheckClient(
        UpstreamConnectionFactory connectionFactory,
        ProxyMetrics? metrics = null)
    {
        _connectionFactory = connectionFactory;
        _metrics = metrics ?? new ProxyMetrics();
    }

    public async ValueTask<HealthCheckSample> CheckAsync(
        RuntimeRoute route,
        RuntimeUpstream upstream,
        CancellationToken cancellationToken)
    {
        if (RuntimeUpstreamProtocol.IsHttp3(upstream.Protocol))
        {
            return await CheckHttp3Async(route, upstream, cancellationToken);
        }

        UpstreamTransportConnection? connection = null;

        try
        {
            connection = await _connectionFactory.ConnectAsync(
                upstream,
                route.HealthCheck.Timeout,
                cancellationToken);
            var stream = connection.Stream;

            if (RuntimeUpstreamProtocol.IsHttp2(upstream.Protocol))
            {
                return await CheckHttp2Async(route, upstream, stream, cancellationToken);
            }

            var requestBytes = Encoding.ASCII.GetBytes(
                $"GET {route.HealthCheck.Path} HTTP/1.1\r\nHost: {upstream.Address}\r\nConnection: close\r\n\r\n");
            await ProxyTimeoutPolicy.RunAsync(
                async timeoutToken => await stream.WriteAsync(requestBytes, timeoutToken),
                route.HealthCheck.Timeout,
                ProxyTimeoutKind.DownstreamWrite,
                cancellationToken);

            var responseHead = await ReadResponseHeadAsync(stream, route.HealthCheck.Timeout, cancellationToken);
            if (responseHead.Length == 0)
            {
                return new HealthCheckSample(false, "empty response");
            }

            if (!Http1ResponseParser.TryParse(responseHead.Span, "GET", out var parsed, out var error))
            {
                return new HealthCheckSample(false, $"malformed response: {error}");
            }

            var healthy = parsed.StatusCode is >= 200 and <= 399;
            return new HealthCheckSample(healthy, $"HTTP {parsed.StatusCode}");
        }
        catch (ProxyTimeoutException)
        {
            return new HealthCheckSample(false, "timeout");
        }
        catch (Exception exception) when (exception is SocketException or IOException)
        {
            return new HealthCheckSample(false, exception.GetType().Name);
        }
        finally
        {
            connection?.Dispose();
        }
    }

    private async ValueTask<HealthCheckSample> CheckHttp3Async(
        RuntimeRoute route,
        RuntimeUpstream upstream,
        CancellationToken cancellationToken)
    {
        try
        {
            var timeouts = HealthCheckTimeouts(route.HealthCheck.Timeout);
            await using var http3 = await Http3UpstreamConnection.ConnectAsync(
                upstream,
                timeouts,
                _metrics,
                maxFramePayloadBytes: 16 * 1024,
                cancellationToken);
            await http3.SendHeadersAsync(
                [
                    new Http1HeaderField(":method", "GET"),
                    new Http1HeaderField(":scheme", upstream.Scheme),
                    new Http1HeaderField(":authority", upstream.Address),
                    new Http1HeaderField(":path", route.HealthCheck.Path)
                ],
                endStream: true,
                timeouts,
                cancellationToken);
            var response = await http3.ReadResponseHeadAsync(MaxHealthResponseHeadBytes, timeouts, cancellationToken);
            var healthy = response.StatusCode is >= 200 and <= 399;
            return new HealthCheckSample(healthy, $"HTTP/3 {response.StatusCode}");
        }
        catch (ProxyTimeoutException)
        {
            return new HealthCheckSample(false, "timeout");
        }
        catch (Http3UpstreamProtocolException)
        {
            return new HealthCheckSample(false, "HTTP/3 protocol error");
        }
        catch (Exception exception) when (exception is SocketException or IOException)
        {
            return new HealthCheckSample(false, exception.GetType().Name);
        }
    }

    private async ValueTask<HealthCheckSample> CheckHttp2Async(
        RuntimeRoute route,
        RuntimeUpstream upstream,
        Stream stream,
        CancellationToken cancellationToken)
    {
        var timeouts = HealthCheckTimeouts(route.HealthCheck.Timeout);
        var http2 = new Http2UpstreamConnection(stream, _metrics, maxFrameSize: 16 * 1024);
        await http2.InitializeAsync(timeouts, cancellationToken);
        await http2.SendHeadersAsync(
            [
                new Http1HeaderField(":method", "GET"),
                new Http1HeaderField(":scheme", upstream.Scheme),
                new Http1HeaderField(":authority", upstream.Address),
                new Http1HeaderField(":path", route.HealthCheck.Path)
            ],
            endStream: true,
            timeouts,
            cancellationToken);
        var response = await http2.ReadResponseHeadAsync(MaxHealthResponseHeadBytes, timeouts, cancellationToken);
        var healthy = response.StatusCode is >= 200 and <= 399;
        return new HealthCheckSample(healthy, $"HTTP/2 {response.StatusCode}");
    }

    private static RuntimeTimeouts HealthCheckTimeouts(TimeSpan timeout)
    {
        return new RuntimeTimeouts(
            timeout,
            timeout,
            timeout,
            timeout,
            timeout,
            timeout,
            timeout,
            timeout,
            timeout,
            timeout);
    }

    private static async ValueTask<ReadOnlyMemory<byte>> ReadResponseHeadAsync(
        Stream stream,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(MaxHealthResponseHeadBytes);
        var total = 0;

        try
        {
            while (total < MaxHealthResponseHeadBytes)
            {
                var bytesRead = await ProxyTimeoutPolicy.RunAsync(
                    async timeoutToken => await stream.ReadAsync(
                        buffer.AsMemory(total, MaxHealthResponseHeadBytes - total),
                        timeoutToken),
                    timeout,
                    ProxyTimeoutKind.UpstreamResponseHead,
                    cancellationToken);
                if (bytesRead == 0)
                {
                    return ReadOnlyMemory<byte>.Empty;
                }

                total += bytesRead;
                var headLength = FindHeadLength(buffer.AsSpan(0, total));
                if (headLength > 0)
                {
                    return buffer.AsMemory(0, headLength).ToArray();
                }
            }

            return ReadOnlyMemory<byte>.Empty;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static int FindHeadLength(ReadOnlySpan<byte> bytes)
    {
        for (var index = 3; index < bytes.Length; index++)
        {
            if (bytes[index - 3] == (byte)'\r'
                && bytes[index - 2] == (byte)'\n'
                && bytes[index - 1] == (byte)'\r'
                && bytes[index] == (byte)'\n')
            {
                return index + 1;
            }
        }

        return -1;
    }
}
