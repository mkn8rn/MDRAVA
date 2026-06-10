using MDRAVA.BLL.ControlPlane.Headers;
using MDRAVA.BLL.ControlPlane.Forwarding;
using MDRAVA.BLL.ControlPlane.Http1;
using MDRAVA.BLL.ControlPlane.Timeouts;
using MDRAVA.BLL.ControlPlane.Upgrades;
using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane;
using MDRAVA.BLL.ControlPlane.Metrics;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using MDRAVA.INF.Proxy.Connections;
using MDRAVA.INF.Observability;

namespace MDRAVA.INF.Proxy.Forwarding;

public sealed class UpgradeForwarder
{
    private const string WebSocketAcceptGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    private readonly UpstreamConnectionFactory _connectionFactory;
    private readonly HopByHopHeaderPolicy _headerPolicy;
    private readonly TunnelRelay _tunnelRelay;
    private readonly ProxyMetrics _metrics;
    private readonly ILogger<UpgradeForwarder> _logger;

    public UpgradeForwarder(
        UpstreamConnectionFactory connectionFactory,
        HopByHopHeaderPolicy headerPolicy,
        TunnelRelay tunnelRelay,
        ProxyMetrics metrics,
        ILogger<UpgradeForwarder> logger)
    {
        _connectionFactory = connectionFactory;
        _headerPolicy = headerPolicy;
        _tunnelRelay = tunnelRelay;
        _metrics = metrics;
        _logger = logger;
    }

    public async ValueTask<ForwardingResult> ForwardAsync(
        Stream clientStream,
        Http1RequestHead requestHead,
        UpgradeRequestInfo upgrade,
        RuntimeRoute route,
        RuntimeUpstream upstream,
        RuntimeListener listener,
        RuntimeTimeouts timeouts,
        RuntimeConnectionLimits connectionLimits,
        string upstreamTarget,
        ForwardedHeadersContext forwardedHeaders,
        string requestId,
        CancellationToken cancellationToken)
    {
        var responseStarted = false;
        UpstreamTransportConnection? upstreamConnection = null;

        try
        {
            if (!_metrics.TryStartTunnel(connectionLimits.MaxActiveUpgradedTunnels))
            {
                _metrics.UpgradeRequestRejected();
                await ProxyErrorResponses.WriteAsync(
                    clientStream,
                    BuildGeneratedServiceUnavailable(requestId),
                    timeouts.DownstreamWriteTimeout,
                    _metrics,
                    cancellationToken);
                return new ForwardingResult(false, responseStarted, false, 503, ProxyFailureKind.UpgradeRejected);
            }

            try
            {
                upstreamConnection = await _connectionFactory.ConnectAsync(
                    upstream,
                    timeouts.UpstreamConnectTimeout,
                    cancellationToken);
                var upstreamStream = upstreamConnection.Stream;

                await WriteUpgradeRequestAsync(upstreamStream, requestHead, upgrade, route, upstreamTarget, forwardedHeaders, timeouts, cancellationToken);
                var responseHeadRead = await ReadResponseHeadAsync(
                    upstreamStream,
                    listener.MaxResponseHeadBytes,
                    timeouts.UpstreamResponseHeadTimeout,
                    cancellationToken);
                if (responseHeadRead.HeadLength <= 0)
                {
                    throw new Http1UpstreamProtocolException("Upstream closed before sending an Upgrade response.");
                }

                if (!Http1ResponseParser.TryParse(
                        responseHeadRead.HeadBytes.Span,
                        requestHead.Method,
                        out var responseHead,
                        out var parseError))
                {
                    throw new Http1UpstreamProtocolException($"Upstream Upgrade response was invalid: {parseError}.");
                }

                if (responseHead.StatusCode != 101)
                {
                    await ForwardNonUpgradeResponseAsync(
                        upstreamStream,
                        clientStream,
                        responseHeadRead.InitialBodyBytes,
                        responseHead,
                        route,
                        listener,
                        timeouts,
                        requestId,
                        cancellationToken);
                    responseStarted = true;
                    return new ForwardingResult(true, responseStarted, false, responseHead.StatusCode);
                }

                if (!IsValidSwitchingProtocolsResponse(responseHead, upgrade))
                {
                    throw new Http1UpstreamProtocolException("Upstream returned an invalid 101 Switching Protocols response.");
                }

                await WriteSwitchingProtocolsResponseAsync(clientStream, responseHead, upgrade, route, timeouts, requestId, cancellationToken);
                responseStarted = true;
                _metrics.UpgradeRequestSucceeded();
                _metrics.TunnelStarted();
                _logger.LogDebug(
                    "Upgraded {Method} {Target} to protocol {Protocol} through upstream {UpstreamName}",
                    requestHead.Method,
                    requestHead.Target,
                    upgrade.Protocol,
                    upstream.Name);
                var tunnelResult = await _tunnelRelay.RelayAsync(clientStream, upstreamStream, listener, timeouts, cancellationToken);
                var failureKind = tunnelResult.CloseReason == "IdleTimeout"
                    ? ProxyFailureKind.TunnelIdleTimeout
                    : tunnelResult.CloseReason == "RelayFailure"
                        ? ProxyFailureKind.TunnelRelayFailure
                        : ProxyFailureKind.None;
                return new ForwardingResult(true, responseStarted, false, 101, failureKind, tunnelResult);
            }
            finally
            {
                _metrics.TunnelClosed();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ProxyTimeoutException exception)
        {
            var timeoutFailure = ProxyTimeoutFailurePolicy.ClassifyForwardingTimeout(exception.Kind, responseStarted);
            await HandleTimeoutAsync(clientStream, requestHead, upstream, responseStarted, exception, timeouts, requestId, cancellationToken);
            return new ForwardingResult(
                false,
                responseStarted,
                false,
                timeoutFailure.ResponseStatusCode,
                timeoutFailure.FailureKind);
        }
        catch (Http1UpstreamProtocolException exception)
        {
            _metrics.UpstreamMalformedResponse();
            _metrics.UpgradeUpstreamFailed();
            _metrics.UpstreamFailed();
            _logger.LogWarning(
                exception,
                "Upstream Upgrade response failed for {Method} {Target} to upstream {UpstreamName}",
                requestHead.Method,
                requestHead.Target,
                upstream.Name);

            if (!responseStarted)
            {
                _metrics.ProxyGenerated502();
                await ProxyErrorResponses.WriteAsync(
                    clientStream,
                    BuildGeneratedBadGateway(requestId),
                    timeouts.DownstreamWriteTimeout,
                    _metrics,
                    cancellationToken);
            }

            return new ForwardingResult(false, responseStarted, false, responseStarted ? null : 502, ProxyFailureKind.UpstreamMalformedResponse);
        }
        catch (Exception exception) when (exception is SocketException or IOException)
        {
            _metrics.UpgradeUpstreamFailed();
            _metrics.UpstreamFailed();
            _logger.LogWarning(
                exception,
                "Upgrade forwarding failed for {Method} {Target} to upstream {UpstreamName}",
                requestHead.Method,
                requestHead.Target,
                upstream.Name);

            if (!responseStarted)
            {
                _metrics.ProxyGenerated502();
                await ProxyErrorResponses.WriteAsync(
                    clientStream,
                    BuildGeneratedBadGateway(requestId),
                    timeouts.DownstreamWriteTimeout,
                    _metrics,
                    cancellationToken);
            }

            return new ForwardingResult(false, responseStarted, false, responseStarted ? null : 502, ProxyFailureKind.UpstreamConnectFailed);
        }
        finally
        {
            upstreamConnection?.Dispose();
        }
    }

    private async ValueTask HandleTimeoutAsync(
        Stream clientStream,
        Http1RequestHead requestHead,
        RuntimeUpstream upstream,
        bool responseStarted,
        ProxyTimeoutException exception,
        RuntimeTimeouts timeouts,
        string requestId,
        CancellationToken cancellationToken)
    {
        switch (exception.Kind)
        {
            case ProxyTimeoutKind.UpstreamConnect:
                _metrics.UpstreamConnectTimedOut();
                _metrics.UpgradeUpstreamFailed();
                _metrics.UpstreamFailed();
                _logger.LogWarning(exception, "Timed out connecting Upgrade request to upstream {UpstreamName}", upstream.Name);
                if (!responseStarted)
                {
                    _metrics.ProxyGenerated504();
                    await ProxyErrorResponses.WriteAsync(clientStream, BuildGeneratedGatewayTimeout(requestId), timeouts.DownstreamWriteTimeout, _metrics, cancellationToken);
                }
                break;
            case ProxyTimeoutKind.UpstreamResponseHead:
                _metrics.UpstreamResponseHeadTimedOut();
                _metrics.UpgradeUpstreamFailed();
                _metrics.UpstreamFailed();
                _logger.LogWarning(exception, "Timed out waiting for upstream Upgrade response head from {UpstreamName}", upstream.Name);
                if (!responseStarted)
                {
                    _metrics.ProxyGenerated504();
                    await ProxyErrorResponses.WriteAsync(clientStream, BuildGeneratedGatewayTimeout(requestId), timeouts.DownstreamWriteTimeout, _metrics, cancellationToken);
                }
                break;
            case ProxyTimeoutKind.UpstreamResponseBodyIdle:
                _metrics.UpstreamResponseBodyTimedOut();
                _metrics.UpstreamFailed();
                _logger.LogWarning(exception, "Timed out relaying non-101 upstream response body from {UpstreamName}", upstream.Name);
                break;
            case ProxyTimeoutKind.DownstreamWrite:
                _metrics.DownstreamWriteTimedOut();
                _logger.LogDebug(exception, "Downstream write timed out for Upgrade {Method} {Target}", requestHead.Method, requestHead.Target);
                break;
        }
    }

    private async ValueTask WriteUpgradeRequestAsync(
        Stream upstreamStream,
        Http1RequestHead requestHead,
        UpgradeRequestInfo upgrade,
        RuntimeRoute route,
        string upstreamTarget,
        ForwardedHeadersContext forwardedHeaders,
        RuntimeTimeouts timeouts,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.Append(requestHead.Method).Append(' ')
            .Append(upstreamTarget).Append(' ')
            .Append("HTTP/1.1").Append("\r\n");

        var filtered = _headerPolicy.FilterForForwarding(
            requestHead.Headers,
            preserveTransferEncoding: false,
            preserveTrailer: false);

        var requestHeaders = ProxyHeaderMutationPolicy.ApplyRequestHeaders(filtered, route.HeaderPolicy, forwardedHeaders);
        foreach (var header in requestHeaders)
        {
            if (UpgradeRequestPolicy.IsManagedUpgradeHeader(header.Name))
            {
                continue;
            }

            builder.Append(header.Name).Append(": ").Append(header.Value).Append("\r\n");
        }

        builder.Append("Upgrade: ").Append(upgrade.Protocol).Append("\r\n");
        builder.Append("Connection: Upgrade\r\n\r\n");
        var bytes = Encoding.ASCII.GetBytes(builder.ToString());
        await WriteWithTimeoutAsync(upstreamStream, bytes, timeouts.DownstreamWriteTimeout, cancellationToken);
        _metrics.AddBytesWritten(bytes.Length);
    }

    private async ValueTask WriteSwitchingProtocolsResponseAsync(
        Stream clientStream,
        Http1ResponseHead responseHead,
        UpgradeRequestInfo upgrade,
        RuntimeRoute route,
        RuntimeTimeouts timeouts,
        string requestId,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.Append(responseHead.Version).Append(' ')
            .Append(responseHead.StatusCode).Append(' ')
            .Append(responseHead.ReasonPhrase).Append("\r\n");

        var responseHeaders = ProxyHeaderMutationPolicy.ApplyResponseHeaders(responseHead.Headers, route.HeaderPolicy);
        foreach (var header in responseHeaders)
        {
            if (UpgradeRequestPolicy.IsManagedUpgradeHeader(header.Name)
                || UpgradeRequestPolicy.IsUnsafeSwitchingProtocolsResponseHeader(header.Name))
            {
                continue;
            }

            builder.Append(header.Name).Append(": ").Append(header.Value).Append("\r\n");
        }

        builder.Append("X-Request-Id: ").Append(requestId).Append("\r\n");
        builder.Append("Upgrade: ").Append(upgrade.Protocol).Append("\r\n");
        builder.Append("Connection: Upgrade\r\n\r\n");
        var bytes = Encoding.ASCII.GetBytes(builder.ToString());
        await WriteWithTimeoutAsync(clientStream, bytes, timeouts.DownstreamWriteTimeout, cancellationToken);
        _metrics.AddBytesWritten(bytes.Length);
    }

    private async ValueTask ForwardNonUpgradeResponseAsync(
        Stream upstreamStream,
        Stream clientStream,
        ReadOnlyMemory<byte> initialBodyBytes,
        Http1ResponseHead responseHead,
        RuntimeRoute route,
        RuntimeListener listener,
        RuntimeTimeouts timeouts,
        string requestId,
        CancellationToken cancellationToken)
    {
        await WriteNonUpgradeResponseHeadAsync(clientStream, responseHead, route, timeouts, requestId, cancellationToken);
        await RelayNonUpgradeResponseBodyAsync(
            upstreamStream,
            clientStream,
            initialBodyBytes,
            responseHead,
            listener,
            timeouts,
            cancellationToken);
    }

    private async ValueTask WriteNonUpgradeResponseHeadAsync(
        Stream clientStream,
        Http1ResponseHead responseHead,
        RuntimeRoute route,
        RuntimeTimeouts timeouts,
        string requestId,
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

        var responseHeaders = ProxyHeaderMutationPolicy.ApplyResponseHeaders(filtered, route.HeaderPolicy);
        foreach (var header in responseHeaders)
        {
            if (Http1ManagedHeaderPolicy.IsManagedFramingHeader(header.Name))
            {
                continue;
            }

            builder.Append(header.Name).Append(": ").Append(header.Value).Append("\r\n");
        }

        builder.Append("X-Request-Id: ").Append(requestId).Append("\r\n");

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

    private async ValueTask RelayNonUpgradeResponseBodyAsync(
        Stream upstreamStream,
        Stream clientStream,
        ReadOnlyMemory<byte> initialBodyBytes,
        Http1ResponseHead responseHead,
        RuntimeListener listener,
        RuntimeTimeouts timeouts,
        CancellationToken cancellationToken)
    {
        var reader = new BodyReader(upstreamStream, initialBodyBytes, _metrics, timeouts.UpstreamResponseBodyIdleTimeout);
        if (responseHead.Framing.Kind == Http1BodyKind.ContentLength)
        {
            await RelayFixedLengthBodyAsync(reader, clientStream, responseHead.Framing.ContentLength.GetValueOrDefault(), listener.ForwardingBufferBytes, timeouts.DownstreamWriteTimeout, cancellationToken);
        }
        else if (responseHead.Framing.Kind == Http1BodyKind.Chunked)
        {
            await RelayChunkedBodyAsync(reader, clientStream, listener, timeouts.DownstreamWriteTimeout, cancellationToken);
        }
        else if (responseHead.Framing.Kind == Http1BodyKind.CloseDelimited)
        {
            await RelayCloseDelimitedBodyAsync(reader, clientStream, listener.ForwardingBufferBytes, timeouts.DownstreamWriteTimeout, cancellationToken);
        }
    }

    private async ValueTask<Http1HeadReadResult> ReadResponseHeadAsync(
        Stream upstreamStream,
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

    private bool IsValidSwitchingProtocolsResponse(
        Http1ResponseHead responseHead,
        UpgradeRequestInfo upgrade)
    {
        if (!HopByHopHeaderPolicy.HasConnectionToken(responseHead.Headers, "upgrade"))
        {
            return false;
        }

        var responseUpgrade = UpgradeRequestPolicy.GetHeaderValue(responseHead.Headers, "Upgrade");
        if (!string.Equals(responseUpgrade, upgrade.Protocol, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!upgrade.IsWebSocket)
        {
            return true;
        }

        var accept = UpgradeRequestPolicy.GetHeaderValue(responseHead.Headers, "Sec-WebSocket-Accept");
        return string.Equals(accept, ComputeWebSocketAccept(upgrade.WebSocketKey!), StringComparison.Ordinal);
    }

    private static string ComputeWebSocketAccept(string webSocketKey)
    {
        var input = Encoding.ASCII.GetBytes(webSocketKey.Trim() + WebSocketAcceptGuid);
        var hash = SHA1.HashData(input);
        return Convert.ToBase64String(hash);
    }

    private async ValueTask RelayFixedLengthBodyAsync(
        BodyReader reader,
        Stream destination,
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
        BodyReader reader,
        Stream destination,
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
        BodyReader reader,
        Stream destination,
        RuntimeListener listener,
        TimeSpan writeTimeout,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var chunkLine = await reader.ReadLineWithCrlfAsync(listener.MaxChunkLineBytes, cancellationToken);
            await WriteWithTimeoutAsync(destination, chunkLine, writeTimeout, cancellationToken);
            _metrics.AddBytesWritten(chunkLine.Length);
            if (!Http1ChunkSizeParser.TryParseLine(chunkLine.AsSpan(), out var chunkSize))
            {
                throw new IOException("Invalid chunk-size line.");
            }

            if (chunkSize == 0)
            {
                await RelayTrailerSectionAsync(reader, destination, listener.MaxChunkLineBytes, writeTimeout, cancellationToken);
                return;
            }

            await RelayFixedLengthBodyAsync(reader, destination, chunkSize, listener.ForwardingBufferBytes, writeTimeout, cancellationToken);
            var crlf = await reader.ReadExactAsync(2, cancellationToken);
            await WriteWithTimeoutAsync(destination, crlf, writeTimeout, cancellationToken);
            _metrics.AddBytesWritten(crlf.Length);
        }
    }

    private async ValueTask RelayTrailerSectionAsync(
        BodyReader reader,
        Stream destination,
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
        }
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

    private static ReadOnlyMemory<byte> BuildGeneratedBadGateway(string requestId)
    {
        return Encoding.ASCII.GetBytes(
            $"HTTP/1.1 502 Bad Gateway\r\nConnection: close\r\nContent-Length: 11\r\nContent-Type: text/plain\r\nX-Request-Id: {requestId}\r\n\r\nBad Gateway");
    }

    private static ReadOnlyMemory<byte> BuildGeneratedGatewayTimeout(string requestId)
    {
        return Encoding.ASCII.GetBytes(
            $"HTTP/1.1 504 Gateway Timeout\r\nConnection: close\r\nContent-Length: 15\r\nContent-Type: text/plain\r\nX-Request-Id: {requestId}\r\n\r\nGateway Timeout");
    }

    private static ReadOnlyMemory<byte> BuildGeneratedServiceUnavailable(string requestId)
    {
        return Encoding.ASCII.GetBytes(
            $"HTTP/1.1 503 Service Unavailable\r\nConnection: close\r\nContent-Length: 19\r\nContent-Type: text/plain\r\nX-Request-Id: {requestId}\r\n\r\nService Unavailable");
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

    private sealed class BodyReader
    {
        private readonly Stream _stream;
        private readonly ProxyMetrics _metrics;
        private readonly TimeSpan _readTimeout;
        private ReadOnlyMemory<byte> _initialBytes;

        public BodyReader(
            Stream stream,
            ReadOnlyMemory<byte> initialBytes,
            ProxyMetrics metrics,
            TimeSpan readTimeout)
        {
            _stream = stream;
            _initialBytes = initialBytes;
            _metrics = metrics;
            _readTimeout = readTimeout;
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
                ProxyTimeoutKind.UpstreamResponseBodyIdle,
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
}
