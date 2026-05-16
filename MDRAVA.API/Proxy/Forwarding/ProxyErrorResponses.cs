using MDRAVA.API.Proxy.Metrics;

namespace MDRAVA.API.Proxy.Forwarding;

public static class ProxyErrorResponses
{
    private static readonly byte[] BadRequestResponse =
        "HTTP/1.1 400 Bad Request\r\nConnection: close\r\nContent-Length: 11\r\nContent-Type: text/plain\r\n\r\nBad Request"u8.ToArray();

    private static readonly byte[] RequestTimeoutResponse =
        "HTTP/1.1 408 Request Timeout\r\nConnection: close\r\nContent-Length: 15\r\nContent-Type: text/plain\r\n\r\nRequest Timeout"u8.ToArray();

    private static readonly byte[] BadGatewayResponse =
        "HTTP/1.1 502 Bad Gateway\r\nConnection: close\r\nContent-Length: 11\r\nContent-Type: text/plain\r\n\r\nBad Gateway"u8.ToArray();

    private static readonly byte[] GatewayTimeoutResponse =
        "HTTP/1.1 504 Gateway Timeout\r\nConnection: close\r\nContent-Length: 15\r\nContent-Type: text/plain\r\n\r\nGateway Timeout"u8.ToArray();

    public static ReadOnlyMemory<byte> BadRequest => BadRequestResponse;

    public static ReadOnlyMemory<byte> RequestTimeout => RequestTimeoutResponse;

    public static ReadOnlyMemory<byte> BadGateway => BadGatewayResponse;

    public static ReadOnlyMemory<byte> GatewayTimeout => GatewayTimeoutResponse;

    public static async ValueTask WriteAsync(
        Stream stream,
        ReadOnlyMemory<byte> response,
        TimeSpan timeout,
        ProxyMetrics metrics,
        CancellationToken cancellationToken)
    {
        await ProxyTimeoutPolicy.RunAsync(
            async timeoutToken =>
            {
                await stream.WriteAsync(response, timeoutToken);
            },
            timeout,
            ProxyTimeoutKind.DownstreamWrite,
            cancellationToken);

        metrics.AddBytesWritten(response.Length);
    }
}
