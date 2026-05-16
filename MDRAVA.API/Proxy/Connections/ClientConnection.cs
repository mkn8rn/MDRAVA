using System.Buffers;
using System.Net.Sockets;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Forwarding;
using MDRAVA.API.Proxy.Metrics;
using MDRAVA.API.Proxy.Protocol;
using MDRAVA.API.Proxy.Routing;

namespace MDRAVA.API.Proxy.Connections;

public sealed class ClientConnection
{
    private const int EmptyRequestHead = 0;
    private const int RequestHeadTooLarge = -1;
    private const int IncompleteRequestHead = -2;

    private static readonly byte[] BadRequestResponse =
        "HTTP/1.1 400 Bad Request\r\nConnection: close\r\nContent-Length: 11\r\nContent-Type: text/plain\r\n\r\nBad Request"u8.ToArray();

    private static readonly byte[] RequestHeaderFieldsTooLargeResponse =
        "HTTP/1.1 431 Request Header Fields Too Large\r\nConnection: close\r\nContent-Length: 22\r\nContent-Type: text/plain\r\n\r\nRequest Head Too Large"u8.ToArray();

    private static readonly byte[] NotImplementedResponse =
        "HTTP/1.1 501 Not Implemented\r\nConnection: close\r\nContent-Length: 15\r\nContent-Type: text/plain\r\n\r\nNot Implemented"u8.ToArray();

    private static readonly byte[] NotFoundResponse =
        "HTTP/1.1 404 Not Found\r\nConnection: close\r\nContent-Length: 9\r\nContent-Type: text/plain\r\n\r\nNot Found"u8.ToArray();

    private readonly Socket _socket;
    private readonly ProxyConfigurationSnapshot _configurationSnapshot;
    private readonly RuntimeListener _listener;
    private readonly IRouteMatcher _routeMatcher;
    private readonly ProxyForwarder _forwarder;
    private readonly ProxyMetrics _metrics;
    private readonly ILogger<ClientConnection> _logger;

    public ClientConnection(
        Socket socket,
        ProxyConfigurationSnapshot configurationSnapshot,
        RuntimeListener listener,
        IRouteMatcher routeMatcher,
        ProxyForwarder forwarder,
        ProxyMetrics metrics,
        ILogger<ClientConnection> logger)
    {
        _socket = socket;
        _configurationSnapshot = configurationSnapshot;
        _listener = listener;
        _routeMatcher = routeMatcher;
        _forwarder = forwarder;
        _metrics = metrics;
        _logger = logger;
    }

    public async ValueTask RunAsync(CancellationToken cancellationToken)
    {
        _socket.NoDelay = true;

        using var clientStream = new NetworkStream(_socket, ownsSocket: true);
        var requestHeadBuffer = ArrayPool<byte>.Shared.Rent(_listener.MaxRequestHeadBytes);

        try
        {
            var requestHeadRead = await ProxyTimeoutPolicy.RunAsync(
                timeoutToken => ReadRequestHeadAsync(clientStream, requestHeadBuffer, timeoutToken),
                _configurationSnapshot.Timeouts.ClientRequestHeadTimeout,
                ProxyTimeoutKind.ClientRequestHead,
                cancellationToken);
            if (requestHeadRead.HeadLength == EmptyRequestHead)
            {
                return;
            }

            if (requestHeadRead.HeadLength == RequestHeadTooLarge)
            {
                _metrics.ParseFailed();
                _metrics.MalformedRequestRejected();
                await WriteResponseAsync(clientStream, RequestHeaderFieldsTooLargeResponse, cancellationToken);
                return;
            }

            if (requestHeadRead.HeadLength == IncompleteRequestHead)
            {
                _metrics.ParseFailed();
                _metrics.MalformedRequestRejected();
                await WriteResponseAsync(clientStream, BadRequestResponse, cancellationToken);
                return;
            }

            var requestHeadBytes = requestHeadRead.HeadBytes;
            if (!Http1RequestParser.TryParse(requestHeadBytes.Span, out var requestHead, out var parseError))
            {
                _metrics.ParseFailed();
                if (parseError == Http1ParseError.UnsupportedTransferEncoding)
                {
                    _metrics.UnsupportedRequestFramingRejected();
                    await WriteResponseAsync(clientStream, NotImplementedResponse, cancellationToken);
                    return;
                }

                _metrics.MalformedRequestRejected();
                _logger.LogDebug("Rejected malformed request head with parse error {ParseError}", parseError);
                await WriteResponseAsync(clientStream, BadRequestResponse, cancellationToken);
                return;
            }

            _metrics.RequestReceived();

            if (IsUnsupportedConnectionMethod(requestHead.Method) || HasUpgradeRequest(requestHead.Headers))
            {
                _metrics.UnsupportedRequestFramingRejected();
                await WriteResponseAsync(clientStream, NotImplementedResponse, cancellationToken);
                return;
            }

            var routeMatch = _routeMatcher.Match(_configurationSnapshot, requestHead);
            if (routeMatch is null)
            {
                await WriteResponseAsync(clientStream, NotFoundResponse, cancellationToken);
                return;
            }

            await _forwarder.ForwardAsync(
                clientStream,
                requestHeadRead,
                requestHead,
                routeMatch.Upstream,
                _listener,
                _configurationSnapshot.Timeouts,
                cancellationToken);
        }
        catch (ProxyTimeoutException exception) when (exception.Kind == ProxyTimeoutKind.ClientRequestHead)
        {
            _metrics.ClientRequestHeadTimedOut();
            _logger.LogDebug(exception, "Client timed out before sending a complete request head.");
            await WriteResponseAsync(clientStream, ProxyErrorResponses.RequestTimeout, cancellationToken);
        }
        catch (ProxyTimeoutException exception) when (exception.Kind == ProxyTimeoutKind.DownstreamWrite)
        {
            _metrics.DownstreamWriteTimedOut();
            _logger.LogDebug(exception, "Timed out while writing a generated response to the client.");
        }
        catch (IOException exception) when (IsClientDisconnect(exception))
        {
            _metrics.ClientPrematureDisconnect();
            _logger.LogDebug(exception, "Client disconnected during request processing.");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(requestHeadBuffer);
        }
    }

    private async ValueTask<Http1HeadReadResult> ReadRequestHeadAsync(
        NetworkStream clientStream,
        byte[] requestHeadBuffer,
        CancellationToken cancellationToken)
    {
        var totalBytesRead = 0;

        while (totalBytesRead < _listener.MaxRequestHeadBytes)
        {
            var bytesRead = await clientStream.ReadAsync(
                requestHeadBuffer.AsMemory(totalBytesRead, _listener.MaxRequestHeadBytes - totalBytesRead),
                cancellationToken);

            if (bytesRead == 0)
            {
                var code = totalBytesRead == 0 ? EmptyRequestHead : IncompleteRequestHead;
                return new Http1HeadReadResult(code, totalBytesRead, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);
            }

            totalBytesRead += bytesRead;
            _metrics.AddBytesRead(bytesRead);

            var requestHeadLength = FindRequestHeadLength(requestHeadBuffer.AsSpan(0, totalBytesRead));
            if (requestHeadLength > 0)
            {
                return new Http1HeadReadResult(
                    requestHeadLength,
                    totalBytesRead,
                    requestHeadBuffer.AsMemory(0, requestHeadLength),
                    requestHeadBuffer.AsMemory(requestHeadLength, totalBytesRead - requestHeadLength));
            }
        }

        return new Http1HeadReadResult(RequestHeadTooLarge, totalBytesRead, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);
    }

    private async ValueTask WriteResponseAsync(
        NetworkStream clientStream,
        ReadOnlyMemory<byte> response,
        CancellationToken cancellationToken)
    {
        await ProxyErrorResponses.WriteAsync(
            clientStream,
            response,
            _configurationSnapshot.Timeouts.DownstreamWriteTimeout,
            _metrics,
            cancellationToken);
    }

    private static bool IsUnsupportedConnectionMethod(string method)
    {
        return string.Equals(method, "CONNECT", StringComparison.Ordinal);
    }

    private static bool HasUpgradeRequest(IReadOnlyList<Http1HeaderField> headers)
    {
        return headers.Any(static header => string.Equals(header.Name, "Upgrade", StringComparison.OrdinalIgnoreCase));
    }

    private static int FindRequestHeadLength(ReadOnlySpan<byte> bytes)
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

    private static bool IsClientDisconnect(IOException exception)
    {
        return exception.InnerException is SocketException;
    }
}
