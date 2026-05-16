using System.Buffers;
using System.Net.Sockets;
using System.Text;
using MDRAVA.API.Proxy.Connections;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Forwarding;
using MDRAVA.API.Proxy.Protocol;

namespace MDRAVA.API.Proxy.Health;

public sealed class UpstreamHealthCheckClient
{
    private const int MaxHealthResponseHeadBytes = 16 * 1024;

    private readonly UpstreamConnectionFactory _connectionFactory;

    public UpstreamHealthCheckClient(UpstreamConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<HealthCheckSample> CheckAsync(
        RuntimeRoute route,
        RuntimeUpstream upstream,
        CancellationToken cancellationToken)
    {
        UpstreamTransportConnection? connection = null;

        try
        {
            connection = await _connectionFactory.ConnectAsync(
                upstream,
                route.HealthCheck.Timeout,
                cancellationToken);
            var stream = connection.Stream;

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
