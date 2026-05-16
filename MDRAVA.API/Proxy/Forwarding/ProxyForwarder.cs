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
        RuntimeTimeouts timeouts,
        CancellationToken cancellationToken)
    {
        var responseStarted = false;

        try
        {
            Http1BodyReader? preReadRequestBodyReader = null;
            byte[]? preReadChunkLine = null;

            if (requestHead.Framing.Kind == Http1BodyKind.Chunked)
            {
                preReadRequestBodyReader = new Http1BodyReader(clientStream, requestHeadRead.InitialBodyBytes, _metrics, timeouts.ClientRequestBodyIdleTimeout, ProxyTimeoutKind.ClientRequestBodyIdle);
                preReadChunkLine = await preReadRequestBodyReader.ReadLineWithCrlfAsync(listener.MaxChunkLineBytes, cancellationToken);
                if (!TryParseChunkSize(preReadChunkLine.AsSpan(), out _))
                {
                    throw new Http1ClientProtocolException("Invalid chunk-size line.");
                }
            }

            using var upstreamSocket = await _upstreamConnections.ConnectAsync(
                upstream,
                timeouts.UpstreamConnectTimeout,
                cancellationToken);
            using var upstreamStream = new NetworkStream(upstreamSocket, ownsSocket: false);

            await WriteRequestHeadAsync(upstreamStream, requestHead, timeouts, cancellationToken);
            await RelayRequestBodyAsync(
                clientStream,
                upstreamStream,
                requestHeadRead.InitialBodyBytes,
                requestHead,
                listener,
                timeouts,
                preReadRequestBodyReader,
                preReadChunkLine,
                cancellationToken);

            responseStarted = await RelayResponseAsync(
                upstreamStream,
                clientStream,
                requestHead.Method,
                listener,
                timeouts,
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
        catch (ProxyTimeoutException exception)
        {
            await HandleTimeoutAsync(clientStream, requestHead, upstream, responseStarted, exception, timeouts, cancellationToken);
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
                await ProxyErrorResponses.WriteAsync(
                    clientStream,
                    ProxyErrorResponses.BadRequest,
                    timeouts.DownstreamWriteTimeout,
                    _metrics,
                    cancellationToken);
            }
        }
        catch (Http1UpstreamProtocolException exception)
        {
            _metrics.UpstreamMalformedResponse();
            _metrics.UpstreamFailed();
            if (!responseStarted)
            {
                _metrics.UpstreamConnectFailed();
            }
            _logger.LogWarning(
                exception,
                "Upstream response framing failed for {Method} {Target} to upstream {UpstreamName}",
                requestHead.Method,
                requestHead.Target,
                upstream.Name);

            if (!responseStarted)
            {
                _metrics.ProxyGenerated502();
                await ProxyErrorResponses.WriteAsync(
                    clientStream,
                    ProxyErrorResponses.BadGateway,
                    timeouts.DownstreamWriteTimeout,
                    _metrics,
                    cancellationToken);
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
                _metrics.ProxyGenerated502();
                await ProxyErrorResponses.WriteAsync(
                    clientStream,
                    ProxyErrorResponses.BadGateway,
                    timeouts.DownstreamWriteTimeout,
                    _metrics,
                    cancellationToken);
            }
        }
    }

    private async ValueTask HandleTimeoutAsync(
        NetworkStream clientStream,
        Http1RequestHead requestHead,
        RuntimeUpstream upstream,
        bool responseStarted,
        ProxyTimeoutException exception,
        RuntimeTimeouts timeouts,
        CancellationToken cancellationToken)
    {
        switch (exception.Kind)
        {
            case ProxyTimeoutKind.ClientRequestBodyIdle:
                _metrics.ClientRequestBodyTimedOut();
                _logger.LogDebug(exception, "Client request body timed out for {Method} {Target}", requestHead.Method, requestHead.Target);
                if (!responseStarted)
                {
                    await ProxyErrorResponses.WriteAsync(clientStream, ProxyErrorResponses.RequestTimeout, timeouts.DownstreamWriteTimeout, _metrics, cancellationToken);
                }
                break;
            case ProxyTimeoutKind.UpstreamConnect:
                _metrics.UpstreamConnectTimedOut();
                _metrics.UpstreamFailed();
                _logger.LogWarning(exception, "Timed out connecting to upstream {UpstreamName}", upstream.Name);
                if (!responseStarted)
                {
                    _metrics.ProxyGenerated504();
                    await ProxyErrorResponses.WriteAsync(clientStream, ProxyErrorResponses.GatewayTimeout, timeouts.DownstreamWriteTimeout, _metrics, cancellationToken);
                }
                break;
            case ProxyTimeoutKind.UpstreamResponseHead:
                _metrics.UpstreamResponseHeadTimedOut();
                _metrics.UpstreamFailed();
                _logger.LogWarning(exception, "Timed out waiting for upstream response head from {UpstreamName}", upstream.Name);
                if (!responseStarted)
                {
                    _metrics.ProxyGenerated504();
                    await ProxyErrorResponses.WriteAsync(clientStream, ProxyErrorResponses.GatewayTimeout, timeouts.DownstreamWriteTimeout, _metrics, cancellationToken);
                }
                break;
            case ProxyTimeoutKind.UpstreamResponseBodyIdle:
                _metrics.UpstreamResponseBodyTimedOut();
                _metrics.UpstreamFailed();
                _logger.LogWarning(exception, "Timed out relaying upstream response body from {UpstreamName}", upstream.Name);
                break;
            case ProxyTimeoutKind.DownstreamWrite:
                _metrics.DownstreamWriteTimedOut();
                _logger.LogDebug(exception, "Downstream write timed out for {Method} {Target}", requestHead.Method, requestHead.Target);
                break;
        }
    }

    private async ValueTask WriteRequestHeadAsync(
        NetworkStream upstreamStream,
        Http1RequestHead requestHead,
        RuntimeTimeouts timeouts,
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
        await WriteWithTimeoutAsync(upstreamStream, bytes, timeouts.DownstreamWriteTimeout, cancellationToken);
        _metrics.AddBytesWritten(bytes.Length);
    }

    private async ValueTask RelayRequestBodyAsync(
        NetworkStream clientStream,
        NetworkStream upstreamStream,
        ReadOnlyMemory<byte> initialBodyBytes,
        Http1RequestHead requestHead,
        RuntimeListener listener,
        RuntimeTimeouts timeouts,
        Http1BodyReader? preReadReader,
        byte[]? preReadChunkLine,
        CancellationToken cancellationToken)
    {
        var reader = preReadReader ?? new Http1BodyReader(clientStream, initialBodyBytes, _metrics, timeouts.ClientRequestBodyIdleTimeout, ProxyTimeoutKind.ClientRequestBodyIdle);
        try
        {
            if (requestHead.Framing.Kind == Http1BodyKind.ContentLength)
            {
                await RelayFixedLengthBodyAsync(reader, upstreamStream, requestHead.Framing.ContentLength.GetValueOrDefault(), listener.ForwardingBufferBytes, timeouts.DownstreamWriteTimeout, cancellationToken);
            }
            else if (requestHead.Framing.Kind == Http1BodyKind.Chunked)
            {
                await RelayChunkedBodyAsync(reader, upstreamStream, listener, timeouts.DownstreamWriteTimeout, preReadChunkLine, cancellationToken);
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
        RuntimeTimeouts timeouts,
        CancellationToken cancellationToken)
    {
        ReadOnlyMemory<byte> initialBodyBytes = ReadOnlyMemory<byte>.Empty;
        var responseStarted = false;

        while (true)
        {
            var responseHeadRead = await ReadResponseHeadAsync(upstreamStream, listener.MaxResponseHeadBytes, timeouts.UpstreamResponseHeadTimeout, cancellationToken);
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

            await WriteResponseHeadAsync(clientStream, responseHead, timeouts, cancellationToken);
            responseStarted = true;
            initialBodyBytes = responseHeadRead.InitialBodyBytes;

            if (!Http1ResponseParser.IsInformational(responseHead))
            {
                await RelayResponseBodyAsync(upstreamStream, clientStream, initialBodyBytes, responseHead, listener, timeouts, cancellationToken);
                return responseStarted;
            }
        }
    }

    private async ValueTask WriteResponseHeadAsync(
        NetworkStream clientStream,
        Http1ResponseHead responseHead,
        RuntimeTimeouts timeouts,
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
        await WriteWithTimeoutAsync(clientStream, bytes, timeouts.DownstreamWriteTimeout, cancellationToken);
        _metrics.AddBytesWritten(bytes.Length);
    }

    private async ValueTask RelayResponseBodyAsync(
        NetworkStream upstreamStream,
        NetworkStream clientStream,
        ReadOnlyMemory<byte> initialBodyBytes,
        Http1ResponseHead responseHead,
        RuntimeListener listener,
        RuntimeTimeouts timeouts,
        CancellationToken cancellationToken)
    {
        var reader = new Http1BodyReader(upstreamStream, initialBodyBytes, _metrics, timeouts.UpstreamResponseBodyIdleTimeout, ProxyTimeoutKind.UpstreamResponseBodyIdle);
        try
        {
            if (responseHead.Framing.Kind == Http1BodyKind.ContentLength)
            {
                await RelayFixedLengthBodyAsync(reader, clientStream, responseHead.Framing.ContentLength.GetValueOrDefault(), listener.ForwardingBufferBytes, timeouts.DownstreamWriteTimeout, cancellationToken);
            }
            else if (responseHead.Framing.Kind == Http1BodyKind.Chunked)
            {
                await RelayChunkedBodyAsync(reader, clientStream, listener, timeouts.DownstreamWriteTimeout, null, cancellationToken);
            }
            else if (responseHead.Framing.Kind == Http1BodyKind.CloseDelimited)
            {
                await RelayCloseDelimitedBodyAsync(reader, clientStream, listener.ForwardingBufferBytes, timeouts.DownstreamWriteTimeout, cancellationToken);
            }
        }
        catch
        {
            _metrics.UpstreamBodyRelayFailed();
            _metrics.UpstreamPrematureDisconnect();
            throw;
        }
    }

    private async ValueTask RelayFixedLengthBodyAsync(
        Http1BodyReader reader,
        NetworkStream destination,
        long contentLength,
        int bufferSize,
        TimeSpan writeTimeout,
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

                await WriteWithTimeoutAsync(destination, buffer.AsMemory(0, bytesRead), writeTimeout, cancellationToken);
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
        TimeSpan writeTimeout,
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

                await WriteWithTimeoutAsync(destination, buffer.AsMemory(0, bytesRead), writeTimeout, cancellationToken);
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
        TimeSpan writeTimeout,
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

            await WriteWithTimeoutAsync(destination, chunkLine, writeTimeout, cancellationToken);
            _metrics.AddBytesWritten(chunkLine.Length);

            if (chunkSize == 0)
            {
                await RelayTrailerSectionAsync(reader, destination, listener.MaxChunkLineBytes, writeTimeout, cancellationToken);
                return;
            }

            await RelayFixedLengthBodyAsync(reader, destination, chunkSize, listener.ForwardingBufferBytes, writeTimeout, cancellationToken);
            var crlf = await reader.ReadExactAsync(2, cancellationToken);
            if (crlf.AsSpan()[0] != (byte)'\r' || crlf.AsSpan()[1] != (byte)'\n')
            {
                throw new Http1ClientProtocolException("Chunk data was not followed by CRLF.");
            }

            await WriteWithTimeoutAsync(destination, crlf, writeTimeout, cancellationToken);
            _metrics.AddBytesWritten(crlf.Length);
            chunkLine = null;
        }
    }

    private async ValueTask RelayTrailerSectionAsync(
        Http1BodyReader reader,
        NetworkStream destination,
        int maxLineBytes,
        TimeSpan writeTimeout,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await reader.ReadLineWithCrlfAsync(maxLineBytes, cancellationToken);
            await WriteWithTimeoutAsync(destination, line, writeTimeout, cancellationToken);
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
        TimeSpan responseHeadTimeout,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(maxResponseHeadBytes);
        var totalBytesRead = 0;

        try
        {
            while (totalBytesRead < maxResponseHeadBytes)
            {
                var bytesRead = await ProxyTimeoutPolicy.RunAsync(
                    async timeoutToken => await upstreamStream.ReadAsync(
                        buffer.AsMemory(totalBytesRead, maxResponseHeadBytes - totalBytesRead),
                        timeoutToken),
                    responseHeadTimeout,
                    ProxyTimeoutKind.UpstreamResponseHead,
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

    private static async ValueTask WriteWithTimeoutAsync(
        Stream destination,
        ReadOnlyMemory<byte> bytes,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await ProxyTimeoutPolicy.RunAsync(
            async timeoutToken =>
            {
                await destination.WriteAsync(bytes, timeoutToken);
            },
            timeout,
            ProxyTimeoutKind.DownstreamWrite,
            cancellationToken);
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
        private readonly TimeSpan _readTimeout;
        private readonly ProxyTimeoutKind _timeoutKind;
        private ReadOnlyMemory<byte> _initialBytes;

        public Http1BodyReader(
            NetworkStream stream,
            ReadOnlyMemory<byte> initialBytes,
            ProxyMetrics metrics,
            TimeSpan readTimeout,
            ProxyTimeoutKind timeoutKind)
        {
            _stream = stream;
            _initialBytes = initialBytes;
            _metrics = metrics;
            _readTimeout = readTimeout;
            _timeoutKind = timeoutKind;
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

            var bytesRead = await ProxyTimeoutPolicy.RunAsync(
                async timeoutToken => await _stream.ReadAsync(destination, timeoutToken),
                _readTimeout,
                _timeoutKind,
                cancellationToken);
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
