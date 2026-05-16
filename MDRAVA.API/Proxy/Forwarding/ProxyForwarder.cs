using System.Buffers;
using System.Net.Sockets;
using System.Text;
using MDRAVA.API.Proxy.Connections;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Metrics;
using MDRAVA.API.Proxy.Protocol;

namespace MDRAVA.API.Proxy.Forwarding;

public sealed class ProxyForwarder
{
    private static readonly byte[] BadGatewayResponse =
        "HTTP/1.1 502 Bad Gateway\r\nConnection: close\r\nContent-Length: 11\r\nContent-Type: text/plain\r\n\r\nBad Gateway"u8.ToArray();

    private static readonly byte[] BadRequestResponse =
        "HTTP/1.1 400 Bad Request\r\nConnection: close\r\nContent-Length: 11\r\nContent-Type: text/plain\r\n\r\nBad Request"u8.ToArray();

    private readonly UpstreamConnectionFactory _upstreamConnections;
    private readonly ProxyMetrics _metrics;
    private readonly HopByHopHeaderPolicy _headerPolicy;
    private readonly ILogger<ProxyForwarder> _logger;

    public ProxyForwarder(
        UpstreamConnectionFactory upstreamConnections,
        ProxyMetrics metrics,
        HopByHopHeaderPolicy headerPolicy,
        ILogger<ProxyForwarder> logger)
    {
        _upstreamConnections = upstreamConnections;
        _metrics = metrics;
        _headerPolicy = headerPolicy;
        _logger = logger;
    }

    public async ValueTask ForwardAsync(
        NetworkStream clientStream,
        Http1HeadReadResult requestHeadRead,
        Http1RequestHead requestHead,
        RuntimeUpstream upstream,
        RuntimeListener listener,
        CancellationToken cancellationToken)
    {
        var responseStarted = false;

        try
        {
            Http1BodyReader? preReadRequestBodyReader = null;
            byte[]? preReadChunkLine = null;

            if (requestHead.Framing.Kind == Http1BodyKind.Chunked)
            {
                preReadRequestBodyReader = new Http1BodyReader(clientStream, requestHeadRead.InitialBodyBytes, _metrics);
                preReadChunkLine = await preReadRequestBodyReader.ReadLineWithCrlfAsync(listener.MaxChunkLineBytes, cancellationToken);
                if (!TryParseChunkSize(preReadChunkLine.AsSpan(), out _))
                {
                    throw new Http1ClientProtocolException("Invalid chunk-size line.");
                }
            }

            using var upstreamSocket = await _upstreamConnections.ConnectAsync(upstream, cancellationToken);
            using var upstreamStream = new NetworkStream(upstreamSocket, ownsSocket: false);

            await WriteRequestHeadAsync(upstreamStream, requestHead, cancellationToken);
            await RelayRequestBodyAsync(
                clientStream,
                upstreamStream,
                requestHeadRead.InitialBodyBytes,
                requestHead,
                listener,
                preReadRequestBodyReader,
                preReadChunkLine,
                cancellationToken);

            responseStarted = await RelayResponseAsync(
                upstreamStream,
                clientStream,
                requestHead.Method,
                listener,
                cancellationToken);

            _metrics.UpstreamSucceeded();
            _logger.LogDebug(
                "Proxied {Method} {Target} to upstream {UpstreamName}",
                requestHead.Method,
                requestHead.Target,
                upstream.Name);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Http1ClientProtocolException exception)
        {
            _metrics.MalformedRequestRejected();
            _logger.LogDebug(
                exception,
                "Rejected malformed request body for {Method} {Target}",
                requestHead.Method,
                requestHead.Target);

            if (!responseStarted)
            {
                await clientStream.WriteAsync(BadRequestResponse, cancellationToken);
                _metrics.AddBytesWritten(BadRequestResponse.Length);
            }
        }
        catch (Http1UpstreamProtocolException exception)
        {
            _metrics.UpstreamMalformedResponse();
            _metrics.UpstreamFailed();
            _logger.LogWarning(
                exception,
                "Upstream response framing failed for {Method} {Target} to upstream {UpstreamName}",
                requestHead.Method,
                requestHead.Target,
                upstream.Name);

            if (!responseStarted)
            {
                await clientStream.WriteAsync(BadGatewayResponse, cancellationToken);
                _metrics.AddBytesWritten(BadGatewayResponse.Length);
            }
        }
        catch (Exception exception) when (exception is SocketException or IOException)
        {
            _metrics.UpstreamFailed();
            _logger.LogWarning(
                exception,
                "Upstream forwarding failed for {Method} {Target} to upstream {UpstreamName}",
                requestHead.Method,
                requestHead.Target,
                upstream.Name);

            if (!responseStarted)
            {
                await clientStream.WriteAsync(BadGatewayResponse, cancellationToken);
                _metrics.AddBytesWritten(BadGatewayResponse.Length);
            }
        }
    }

    private async ValueTask WriteRequestHeadAsync(
        NetworkStream upstreamStream,
        Http1RequestHead requestHead,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.Append(requestHead.Method).Append(' ')
            .Append(requestHead.Target).Append(' ')
            .Append(requestHead.Version).Append("\r\n");

        var filtered = _headerPolicy.FilterForForwarding(
            requestHead.Headers,
            preserveTransferEncoding: false,
            preserveTrailer: requestHead.Framing.Kind == Http1BodyKind.Chunked);

        foreach (var header in filtered)
        {
            if (IsManagedFramingHeader(header.Name))
            {
                continue;
            }

            builder.Append(header.Name).Append(": ").Append(header.Value).Append("\r\n");
        }

        if (requestHead.Framing.Kind == Http1BodyKind.ContentLength)
        {
            builder.Append("Content-Length: ").Append(requestHead.Framing.ContentLength.GetValueOrDefault()).Append("\r\n");
        }
        else if (requestHead.Framing.Kind == Http1BodyKind.Chunked)
        {
            builder.Append("Transfer-Encoding: chunked\r\n");
        }

        builder.Append("Connection: close\r\n\r\n");
        var bytes = Encoding.ASCII.GetBytes(builder.ToString());
        await upstreamStream.WriteAsync(bytes, cancellationToken);
        _metrics.AddBytesWritten(bytes.Length);
    }

    private async ValueTask RelayRequestBodyAsync(
        NetworkStream clientStream,
        NetworkStream upstreamStream,
        ReadOnlyMemory<byte> initialBodyBytes,
        Http1RequestHead requestHead,
        RuntimeListener listener,
        Http1BodyReader? preReadReader,
        byte[]? preReadChunkLine,
        CancellationToken cancellationToken)
    {
        var reader = preReadReader ?? new Http1BodyReader(clientStream, initialBodyBytes, _metrics);
        try
        {
            if (requestHead.Framing.Kind == Http1BodyKind.ContentLength)
            {
                await RelayFixedLengthBodyAsync(reader, upstreamStream, requestHead.Framing.ContentLength.GetValueOrDefault(), listener.ForwardingBufferBytes, cancellationToken);
            }
            else if (requestHead.Framing.Kind == Http1BodyKind.Chunked)
            {
                await RelayChunkedBodyAsync(reader, upstreamStream, listener, preReadChunkLine, cancellationToken);
            }
        }
        catch
        {
            _metrics.ClientBodyRelayFailed();
            throw;
        }
    }

    private async ValueTask<bool> RelayResponseAsync(
        NetworkStream upstreamStream,
        NetworkStream clientStream,
        string requestMethod,
        RuntimeListener listener,
        CancellationToken cancellationToken)
    {
        ReadOnlyMemory<byte> initialBodyBytes = ReadOnlyMemory<byte>.Empty;
        var responseStarted = false;

        while (true)
        {
            var responseHeadRead = await ReadResponseHeadAsync(upstreamStream, listener.MaxResponseHeadBytes, cancellationToken);
            if (responseHeadRead.HeadLength <= 0)
            {
                throw new Http1UpstreamProtocolException("Upstream closed before a complete response head was received.");
            }

            if (!Http1ResponseParser.TryParse(
                    responseHeadRead.HeadBytes.Span,
                    requestMethod,
                    out var responseHead,
                    out var error))
            {
                throw new Http1UpstreamProtocolException($"Upstream response head was invalid: {error}.");
            }

            await WriteResponseHeadAsync(clientStream, responseHead, cancellationToken);
            responseStarted = true;
            initialBodyBytes = responseHeadRead.InitialBodyBytes;

            if (!Http1ResponseParser.IsInformational(responseHead))
            {
                await RelayResponseBodyAsync(upstreamStream, clientStream, initialBodyBytes, responseHead, listener, cancellationToken);
                return responseStarted;
            }
        }
    }

    private async ValueTask WriteResponseHeadAsync(
        NetworkStream clientStream,
        Http1ResponseHead responseHead,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.Append(responseHead.Version).Append(' ')
            .Append(responseHead.StatusCode).Append(' ')
            .Append(responseHead.ReasonPhrase).Append("\r\n");

        var filtered = _headerPolicy.FilterForForwarding(
            responseHead.Headers,
            preserveTransferEncoding: false,
            preserveTrailer: responseHead.Framing.Kind == Http1BodyKind.Chunked);

        foreach (var header in filtered)
        {
            if (IsManagedFramingHeader(header.Name))
            {
                continue;
            }

            builder.Append(header.Name).Append(": ").Append(header.Value).Append("\r\n");
        }

        if (responseHead.Framing.Kind == Http1BodyKind.ContentLength)
        {
            builder.Append("Content-Length: ").Append(responseHead.Framing.ContentLength.GetValueOrDefault()).Append("\r\n");
        }
        else if (responseHead.Framing.Kind == Http1BodyKind.Chunked)
        {
            builder.Append("Transfer-Encoding: chunked\r\n");
        }

        builder.Append("Connection: close\r\n\r\n");
        var bytes = Encoding.ASCII.GetBytes(builder.ToString());
        await clientStream.WriteAsync(bytes, cancellationToken);
        _metrics.AddBytesWritten(bytes.Length);
    }

    private async ValueTask RelayResponseBodyAsync(
        NetworkStream upstreamStream,
        NetworkStream clientStream,
        ReadOnlyMemory<byte> initialBodyBytes,
        Http1ResponseHead responseHead,
        RuntimeListener listener,
        CancellationToken cancellationToken)
    {
        var reader = new Http1BodyReader(upstreamStream, initialBodyBytes, _metrics);
        try
        {
            if (responseHead.Framing.Kind == Http1BodyKind.ContentLength)
            {
                await RelayFixedLengthBodyAsync(reader, clientStream, responseHead.Framing.ContentLength.GetValueOrDefault(), listener.ForwardingBufferBytes, cancellationToken);
            }
            else if (responseHead.Framing.Kind == Http1BodyKind.Chunked)
            {
                await RelayChunkedBodyAsync(reader, clientStream, listener, initialChunkLine: null, cancellationToken);
            }
            else if (responseHead.Framing.Kind == Http1BodyKind.CloseDelimited)
            {
                await RelayCloseDelimitedBodyAsync(reader, clientStream, listener.ForwardingBufferBytes, cancellationToken);
            }
        }
        catch
        {
            _metrics.UpstreamBodyRelayFailed();
            throw;
        }
    }

    private async ValueTask RelayFixedLengthBodyAsync(
        Http1BodyReader reader,
        NetworkStream destination,
        long contentLength,
        int bufferSize,
        CancellationToken cancellationToken)
    {
        var remaining = contentLength;
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            while (remaining > 0)
            {
                var readLength = (int)Math.Min(buffer.Length, remaining);
                var bytesRead = await reader.ReadAsync(buffer.AsMemory(0, readLength), cancellationToken);
                if (bytesRead == 0)
                {
                    throw new IOException("Source closed before the declared Content-Length body was complete.");
                }

                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                _metrics.AddBytesWritten(bytesRead);
                remaining -= bytesRead;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async ValueTask RelayCloseDelimitedBodyAsync(
        Http1BodyReader reader,
        NetworkStream destination,
        int bufferSize,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            while (true)
            {
                var bytesRead = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (bytesRead == 0)
                {
                    break;
                }

                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                _metrics.AddBytesWritten(bytesRead);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async ValueTask RelayChunkedBodyAsync(
        Http1BodyReader reader,
        NetworkStream destination,
        RuntimeListener listener,
        byte[]? initialChunkLine,
        CancellationToken cancellationToken)
    {
        var chunkLine = initialChunkLine;
        while (true)
        {
            chunkLine ??= await reader.ReadLineWithCrlfAsync(listener.MaxChunkLineBytes, cancellationToken);
            if (!TryParseChunkSize(chunkLine.AsSpan(), out var chunkSize))
            {
                throw new Http1ClientProtocolException("Invalid chunk-size line.");
            }

            await destination.WriteAsync(chunkLine, cancellationToken);
            _metrics.AddBytesWritten(chunkLine.Length);

            if (chunkSize == 0)
            {
                await RelayTrailerSectionAsync(reader, destination, listener.MaxChunkLineBytes, cancellationToken);
                return;
            }

            await RelayFixedLengthBodyAsync(reader, destination, chunkSize, listener.ForwardingBufferBytes, cancellationToken);
            var crlf = await reader.ReadExactAsync(2, cancellationToken);
            if (crlf.AsSpan()[0] != (byte)'\r' || crlf.AsSpan()[1] != (byte)'\n')
            {
                throw new Http1ClientProtocolException("Chunk data was not followed by CRLF.");
            }

            await destination.WriteAsync(crlf, cancellationToken);
            _metrics.AddBytesWritten(crlf.Length);
            chunkLine = null;
        }
    }

    private async ValueTask RelayTrailerSectionAsync(
        Http1BodyReader reader,
        NetworkStream destination,
        int maxLineBytes,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await reader.ReadLineWithCrlfAsync(maxLineBytes, cancellationToken);
            await destination.WriteAsync(line, cancellationToken);
            _metrics.AddBytesWritten(line.Length);

            if (line.Length == 2)
            {
                return;
            }

            var colon = line.AsSpan()[..^2].IndexOf((byte)':');
            if (colon <= 0)
            {
                throw new Http1ClientProtocolException("Invalid trailer field line.");
            }
        }
    }

    private async ValueTask<Http1HeadReadResult> ReadResponseHeadAsync(
        NetworkStream upstreamStream,
        int maxResponseHeadBytes,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(maxResponseHeadBytes);
        var totalBytesRead = 0;

        try
        {
            while (totalBytesRead < maxResponseHeadBytes)
            {
                var bytesRead = await upstreamStream.ReadAsync(
                    buffer.AsMemory(totalBytesRead, maxResponseHeadBytes - totalBytesRead),
                    cancellationToken);

                if (bytesRead == 0)
                {
                    return new Http1HeadReadResult(-1, totalBytesRead, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);
                }

                totalBytesRead += bytesRead;
                _metrics.AddBytesRead(bytesRead);

                var headLength = FindHeadLength(buffer.AsSpan(0, totalBytesRead));
                if (headLength > 0)
                {
                    var headBytes = buffer.AsMemory(0, headLength).ToArray();
                    var initialBody = buffer.AsMemory(headLength, totalBytesRead - headLength).ToArray();
                    return new Http1HeadReadResult(headLength, totalBytesRead, headBytes, initialBody);
                }
            }

            return new Http1HeadReadResult(-1, totalBytesRead, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static bool TryParseChunkSize(ReadOnlySpan<byte> lineWithCrlf, out long chunkSize)
    {
        chunkSize = 0;
        if (lineWithCrlf.Length < 3 || lineWithCrlf[^2] != (byte)'\r' || lineWithCrlf[^1] != (byte)'\n')
        {
            return false;
        }

        var line = lineWithCrlf[..^2];
        var semicolon = line.IndexOf((byte)';');
        var sizeBytes = semicolon >= 0 ? line[..semicolon] : line;
        if (sizeBytes.Length == 0)
        {
            return false;
        }

        foreach (var value in sizeBytes)
        {
            var digit = HexValue(value);
            if (digit < 0)
            {
                return false;
            }

            if (chunkSize > (long.MaxValue - digit) / 16)
            {
                return false;
            }

            chunkSize = chunkSize * 16 + digit;
        }

        return true;
    }

    private static int HexValue(byte value)
    {
        if (value is >= (byte)'0' and <= (byte)'9')
        {
            return value - (byte)'0';
        }

        if (value is >= (byte)'a' and <= (byte)'f')
        {
            return value - (byte)'a' + 10;
        }

        if (value is >= (byte)'A' and <= (byte)'F')
        {
            return value - (byte)'A' + 10;
        }

        return -1;
    }

    private static bool IsManagedFramingHeader(string headerName)
    {
        return string.Equals(headerName, "Content-Length", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "Connection", StringComparison.OrdinalIgnoreCase);
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

    private sealed class Http1BodyReader
    {
        private readonly NetworkStream _stream;
        private readonly ProxyMetrics _metrics;
        private ReadOnlyMemory<byte> _initialBytes;

        public Http1BodyReader(NetworkStream stream, ReadOnlyMemory<byte> initialBytes, ProxyMetrics metrics)
        {
            _stream = stream;
            _initialBytes = initialBytes;
            _metrics = metrics;
        }

        public async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken)
        {
            if (_initialBytes.Length > 0)
            {
                var bytesToCopy = Math.Min(destination.Length, _initialBytes.Length);
                _initialBytes[..bytesToCopy].CopyTo(destination);
                _initialBytes = _initialBytes[bytesToCopy..];
                return bytesToCopy;
            }

            var bytesRead = await _stream.ReadAsync(destination, cancellationToken);
            _metrics.AddBytesRead(bytesRead);
            return bytesRead;
        }

        public async ValueTask<byte[]> ReadExactAsync(int length, CancellationToken cancellationToken)
        {
            var bytes = new byte[length];
            var total = 0;

            while (total < length)
            {
                var bytesRead = await ReadAsync(bytes.AsMemory(total, length - total), cancellationToken);
                if (bytesRead == 0)
                {
                    throw new IOException("Source closed before the required bytes were read.");
                }

                total += bytesRead;
            }

            return bytes;
        }

        public async ValueTask<byte[]> ReadLineWithCrlfAsync(int maxLineBytes, CancellationToken cancellationToken)
        {
            List<byte> bytes = [];
            var previous = (byte)0;

            while (bytes.Count < maxLineBytes)
            {
                var one = await ReadExactAsync(1, cancellationToken);
                var current = one[0];
                bytes.Add(current);

                if (previous == (byte)'\r' && current == (byte)'\n')
                {
                    return bytes.ToArray();
                }

                previous = current;
            }

            throw new IOException("HTTP line exceeded the configured maximum length.");
        }
    }

    private sealed class Http1UpstreamProtocolException : IOException
    {
        public Http1UpstreamProtocolException(string message)
            : base(message)
        {
        }
    }

    private sealed class Http1ClientProtocolException : IOException
    {
        public Http1ClientProtocolException(string message)
            : base(message)
        {
        }
    }
}
