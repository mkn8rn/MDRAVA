using MDRAVA.BLL.Http;
using MDRAVA.BLL.ControlPlane.Headers;
using MDRAVA.BLL.ControlPlane.Forwarding;
using MDRAVA.BLL.ControlPlane.Timeouts;
using System.Text;
using MDRAVA.BLL.ControlPlane.Metrics;

namespace MDRAVA.INF.Proxy.Forwarding;

public static class ProxyErrorResponses
{
    public static ValueTask WriteGeneratedFailureAsync(
        Stream stream,
        ProxyGeneratedFailureResponse response,
        string? requestId,
        TimeSpan timeout,
        ProxyMetrics metrics,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        return WriteGeneratedAsync(
            stream,
            response.StatusCode,
            response.ReasonPhrase,
            response.Body,
            requestId,
            timeout,
            metrics,
            cancellationToken,
            ProxyGeneratedFailurePolicy.PlainTextContentType,
            []);
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
