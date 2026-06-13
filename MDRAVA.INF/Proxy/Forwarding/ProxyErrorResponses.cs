using MDRAVA.BLL.Http;
using MDRAVA.BLL.ControlPlane.Headers;
using MDRAVA.BLL.ControlPlane.Timeouts;
using System.Text;
using MDRAVA.BLL.ControlPlane.Metrics;

namespace MDRAVA.INF.Proxy.Forwarding;

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

    private static readonly byte[] ServiceUnavailableResponse =
        "HTTP/1.1 503 Service Unavailable\r\nConnection: close\r\nContent-Length: 19\r\nContent-Type: text/plain\r\n\r\nService Unavailable"u8.ToArray();

    public static ReadOnlyMemory<byte> BadRequest => BadRequestResponse;

    public static ReadOnlyMemory<byte> RequestTimeout => RequestTimeoutResponse;

    public static ReadOnlyMemory<byte> BadGateway => BadGatewayResponse;

    public static ReadOnlyMemory<byte> GatewayTimeout => GatewayTimeoutResponse;

    public static ReadOnlyMemory<byte> ServiceUnavailable => ServiceUnavailableResponse;

    public static ReadOnlyMemory<byte> BadRequestWithRequestId(string requestId)
    {
        return Encoding.ASCII.GetBytes(
            $"HTTP/1.1 400 Bad Request\r\nConnection: close\r\nContent-Length: 11\r\nContent-Type: text/plain\r\nX-Request-Id: {requestId}\r\n\r\nBad Request");
    }

    public static ReadOnlyMemory<byte> RequestTimeoutWithRequestId(string requestId)
    {
        return Encoding.ASCII.GetBytes(
            $"HTTP/1.1 408 Request Timeout\r\nConnection: close\r\nContent-Length: 15\r\nContent-Type: text/plain\r\nX-Request-Id: {requestId}\r\n\r\nRequest Timeout");
    }

    public static ReadOnlyMemory<byte> BadGatewayWithRequestId(string requestId)
    {
        return Encoding.ASCII.GetBytes(
            $"HTTP/1.1 502 Bad Gateway\r\nConnection: close\r\nContent-Length: 11\r\nContent-Type: text/plain\r\nX-Request-Id: {requestId}\r\n\r\nBad Gateway");
    }

    public static ReadOnlyMemory<byte> GatewayTimeoutWithRequestId(string requestId)
    {
        return Encoding.ASCII.GetBytes(
            $"HTTP/1.1 504 Gateway Timeout\r\nConnection: close\r\nContent-Length: 15\r\nContent-Type: text/plain\r\nX-Request-Id: {requestId}\r\n\r\nGateway Timeout");
    }

    public static ReadOnlyMemory<byte> PayloadTooLargeWithRequestId(string requestId)
    {
        return Encoding.ASCII.GetBytes(
            $"HTTP/1.1 413 Payload Too Large\r\nConnection: close\r\nContent-Length: 17\r\nContent-Type: text/plain\r\nX-Request-Id: {requestId}\r\n\r\nPayload Too Large");
    }

    public static ReadOnlyMemory<byte> ServiceUnavailableWithRequestId(string requestId)
    {
        return Encoding.ASCII.GetBytes(
            $"HTTP/1.1 503 Service Unavailable\r\nConnection: close\r\nContent-Length: 19\r\nContent-Type: text/plain\r\nX-Request-Id: {requestId}\r\n\r\nService Unavailable");
    }

    public static ValueTask WriteGeneratedAsync(
        Stream stream,
        int statusCode,
        string reasonPhrase,
        string body,
        string? requestId,
        TimeSpan timeout,
        ProxyMetrics metrics,
        CancellationToken cancellationToken,
        string? contentType,
        IReadOnlyList<ProxyHeaderField> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var builder = new StringBuilder();
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        builder.Append("HTTP/1.1 ")
            .Append(statusCode)
            .Append(' ')
            .Append(reasonPhrase)
            .Append("\r\nConnection: close\r\n");
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            builder.Append("Content-Type: ").Append(contentType).Append("\r\n");
        }

        if (!string.IsNullOrWhiteSpace(requestId))
        {
            builder.Append("X-Request-Id: ").Append(requestId).Append("\r\n");
        }

        foreach (var header in headers)
        {
            builder.Append(header.Name).Append(": ").Append(header.Value).Append("\r\n");
        }

        builder.Append("Content-Length: ")
            .Append(bodyBytes.Length)
            .Append("\r\n\r\n");

        var headBytes = Encoding.ASCII.GetBytes(builder.ToString());
        var response = new byte[headBytes.Length + bodyBytes.Length];
        headBytes.CopyTo(response, 0);
        bodyBytes.CopyTo(response, headBytes.Length);
        return WriteAsync(
            stream,
            response,
            timeout,
            metrics,
            cancellationToken);
    }

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
