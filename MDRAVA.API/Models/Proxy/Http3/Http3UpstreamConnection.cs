#pragma warning disable CA1416
using System.Globalization;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Forwarding;
using MDRAVA.API.Proxy.Metrics;
using MDRAVA.API.Proxy.Protocol;

namespace MDRAVA.API.Proxy.Http3;

internal sealed class Http3UpstreamConnection : IAsyncDisposable
{
    private const int MaxFramePayloadBytes = 1024 * 1024;
    private static readonly SslApplicationProtocol Http3Alpn = new("h3");

    private readonly ProxyMetrics _metrics;
    private readonly int _maxFramePayloadBytes;

    private Http3UpstreamConnection(
        QuicConnection connection,
        QuicStream stream,
        ProxyMetrics metrics,
        int maxFramePayloadBytes)
    {
        Connection = connection;
        Stream = stream;
        _metrics = metrics;
        _maxFramePayloadBytes = Math.Clamp(maxFramePayloadBytes, 16 * 1024, MaxFramePayloadBytes);
    }

    private QuicConnection Connection { get; }

    private QuicStream Stream { get; }

    public static async ValueTask<Http3UpstreamConnection> ConnectAsync(
        RuntimeUpstream upstream,
        RuntimeTimeouts timeouts,
        ProxyMetrics metrics,
        int maxFramePayloadBytes,
        CancellationToken cancellationToken)
    {
        metrics.UpstreamHttp3ConnectionAttempted();
        if (!QuicConnection.IsSupported)
        {
            metrics.UpstreamHttp3ConnectionFailed();
            throw new Http3UpstreamProtocolException(
                "The current runtime does not support QUIC client connections.",
                Http3UpstreamFailureKind.ConnectFailure);
        }

        var remoteEndPoint = await ResolveEndPointAsync(upstream, cancellationToken);
        QuicConnection? connection = null;
        QuicStream? stream = null;
        var connectionOpened = false;
        var streamStarted = false;
        try
        {
            connection = await ProxyTimeoutPolicy.RunAsync(
                async timeoutToken => await QuicConnection.ConnectAsync(
                    new QuicClientConnectionOptions
                    {
                        RemoteEndPoint = remoteEndPoint,
                        ClientAuthenticationOptions = new SslClientAuthenticationOptions
                        {
                            TargetHost = upstream.EffectiveSniHost,
                            EnabledSslProtocols = SslProtocols.Tls13,
                            ApplicationProtocols = [Http3Alpn],
                            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                            RemoteCertificateValidationCallback = upstream.Tls.ValidateCertificate
                                ? null
                                : static (_, _, _, _) => true
                        },
                        MaxInboundBidirectionalStreams = 16,
                        MaxInboundUnidirectionalStreams = 4,
                        IdleTimeout = timeouts.UpstreamIdleConnectionLifetime,
                        HandshakeTimeout = timeouts.UpstreamConnectTimeout,
                        DefaultCloseErrorCode = 0x100,
                        DefaultStreamErrorCode = 0x100
                    },
                    timeoutToken),
                timeouts.UpstreamConnectTimeout,
                ProxyTimeoutKind.UpstreamConnect,
                cancellationToken);
            metrics.UpstreamHttp3ConnectionSucceeded();
            metrics.UpstreamHttp3ConnectionOpened();
            metrics.UpstreamHttp3PoolConnectionOpened();
            connectionOpened = true;
            await SendSettingsAsync(connection, timeouts, cancellationToken);
            stream = await ProxyTimeoutPolicy.RunAsync(
                async timeoutToken => await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, timeoutToken),
                timeouts.UpstreamConnectTimeout,
                ProxyTimeoutKind.UpstreamConnect,
                cancellationToken);
            metrics.UpstreamHttp3StreamStarted();
            streamStarted = true;
            return new Http3UpstreamConnection(connection, stream, metrics, maxFramePayloadBytes);
        }
        catch (Http3UpstreamProtocolException)
        {
            metrics.UpstreamHttp3ConnectionFailed();
            await DisposePartialConnectionAsync(connection, stream, metrics, connectionOpened, streamStarted);

            throw;
        }
        catch (Exception exception) when (exception is AuthenticationException or IOException or QuicException)
        {
            metrics.UpstreamHttp3ConnectionFailed();
            await DisposePartialConnectionAsync(connection, stream, metrics, connectionOpened, streamStarted);

            throw new Http3UpstreamProtocolException(
                "Failed to connect to the upstream HTTP/3 endpoint.",
                Http3UpstreamFailureKind.ConnectFailure,
                exception);
        }
    }

    public async ValueTask SendHeadersAsync(
        IReadOnlyList<Http1HeaderField> headers,
        bool endStream,
        RuntimeTimeouts timeouts,
        CancellationToken cancellationToken)
    {
        var block = Http3PreviewCodec.EncodeHeaderBlock(headers);
        using var memory = new MemoryStream();
        Http3PreviewCodec.WriteFrame(memory, Http3PreviewCodec.HeadersFrame, block);
        await WriteWithTimeoutAsync(memory.ToArray(), endStream, timeouts.DownstreamWriteTimeout, cancellationToken);
    }

    public async ValueTask SendDataAsync(
        ReadOnlyMemory<byte> body,
        bool endStream,
        RuntimeTimeouts timeouts,
        CancellationToken cancellationToken)
    {
        var remaining = body;
        while (remaining.Length > 0)
        {
            var chunkLength = Math.Min(_maxFramePayloadBytes, remaining.Length);
            var final = chunkLength == remaining.Length && endStream;
            using var memory = new MemoryStream();
            Http3PreviewCodec.WriteFrame(memory, Http3PreviewCodec.DataFrame, remaining[..chunkLength].Span);
            await WriteWithTimeoutAsync(memory.ToArray(), final, timeouts.DownstreamWriteTimeout, cancellationToken);
            remaining = remaining[chunkLength..];
        }

        if (body.Length == 0 && endStream)
        {
            using var memory = new MemoryStream();
            Http3PreviewCodec.WriteFrame(memory, Http3PreviewCodec.DataFrame, ReadOnlySpan<byte>.Empty);
            await WriteWithTimeoutAsync(memory.ToArray(), completeWrites: true, timeouts.DownstreamWriteTimeout, cancellationToken);
        }
    }

    public async ValueTask<Http3UpstreamResponseHead> ReadResponseHeadAsync(
        int maxHeaderListBytes,
        RuntimeTimeouts timeouts,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var frame = await ReadFrameAsync(
                timeouts.UpstreamResponseHeadTimeout,
                ProxyTimeoutKind.UpstreamResponseHead,
                cancellationToken);
            if (frame.EndStream)
            {
                throw new Http3UpstreamProtocolException("Upstream closed before HTTP/3 response headers were received.");
            }

            if (frame.Type == Http3PreviewCodec.DataFrame)
            {
                throw new Http3UpstreamProtocolException("Upstream sent HTTP/3 response DATA before response headers.");
            }

            if (frame.Type != Http3PreviewCodec.HeadersFrame)
            {
                throw new Http3UpstreamProtocolException("Upstream sent an unsupported HTTP/3 response frame before headers.");
            }

            return DecodeResponseHeaders(frame.Payload.Span, maxHeaderListBytes);
        }
    }

    public async ValueTask<Http3UpstreamDataChunk> ReadDataAsync(
        RuntimeTimeouts timeouts,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var frame = await ReadFrameAsync(
                timeouts.UpstreamResponseBodyIdleTimeout,
                ProxyTimeoutKind.UpstreamResponseBodyIdle,
                cancellationToken);
            if (frame.EndStream)
            {
                return new Http3UpstreamDataChunk([], EndStream: true);
            }

            if (frame.Type == Http3PreviewCodec.DataFrame)
            {
                return new Http3UpstreamDataChunk(frame.Payload.ToArray(), EndStream: false);
            }

            if (frame.Type == Http3PreviewCodec.HeadersFrame)
            {
                continue;
            }

            throw new Http3UpstreamProtocolException("Upstream sent an unsupported HTTP/3 response frame.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Stream.DisposeAsync();
        }
        finally
        {
            _metrics.UpstreamHttp3StreamEnded();
            try
            {
                await Connection.CloseAsync(0, CancellationToken.None);
            }
            catch (QuicException)
            {
            }

            await Connection.DisposeAsync();
            _metrics.UpstreamHttp3ConnectionClosed();
            _metrics.UpstreamHttp3PoolConnectionClosed();
        }
    }

    private static async ValueTask SendSettingsAsync(
        QuicConnection connection,
        RuntimeTimeouts timeouts,
        CancellationToken cancellationToken)
    {
        await using var control = await ProxyTimeoutPolicy.RunAsync(
            async timeoutToken => await connection.OpenOutboundStreamAsync(QuicStreamType.Unidirectional, timeoutToken),
            timeouts.UpstreamConnectTimeout,
            ProxyTimeoutKind.UpstreamConnect,
            cancellationToken);
        using var payload = new MemoryStream();
        Http3PreviewCodec.WriteVarInt(payload, Http3PreviewCodec.ControlStream);
        using var settings = new MemoryStream();
        Http3PreviewCodec.WriteVarInt(settings, Http3PreviewCodec.QpackMaxTableCapacitySetting);
        Http3PreviewCodec.WriteVarInt(settings, 0);
        Http3PreviewCodec.WriteVarInt(settings, Http3PreviewCodec.QpackBlockedStreamsSetting);
        Http3PreviewCodec.WriteVarInt(settings, 0);
        Http3PreviewCodec.WriteFrame(payload, Http3PreviewCodec.SettingsFrame, settings.ToArray());
        await ProxyTimeoutPolicy.RunAsync(
            async timeoutToken => await control.WriteAsync(payload.ToArray(), completeWrites: true, timeoutToken),
            timeouts.DownstreamWriteTimeout,
            ProxyTimeoutKind.DownstreamWrite,
            cancellationToken);
    }

    private async ValueTask<Http3FrameReadResult> ReadFrameAsync(
        TimeSpan timeout,
        ProxyTimeoutKind timeoutKind,
        CancellationToken cancellationToken)
    {
        var type = await ReadVarIntAsync(timeout, timeoutKind, cancellationToken);
        if (!type.Success)
        {
            return Http3FrameReadResult.End;
        }

        var length = await ReadVarIntAsync(timeout, timeoutKind, cancellationToken);
        if (!length.Success
            || length.Value < 0
            || length.Value > _maxFramePayloadBytes)
        {
            throw new Http3UpstreamProtocolException("Upstream HTTP/3 frame was malformed or exceeded the configured maximum size.");
        }

        var payload = length.Value == 0
            ? []
            : await ReadExactAsync((int)length.Value, timeout, timeoutKind, cancellationToken);
        return new Http3FrameReadResult(false, type.Value, payload);
    }

    private async ValueTask<Http3VarIntReadResult> ReadVarIntAsync(
        TimeSpan timeout,
        ProxyTimeoutKind timeoutKind,
        CancellationToken cancellationToken)
    {
        var first = await ReadExactAsync(1, timeout, timeoutKind, cancellationToken, allowEnd: true);
        if (first.Length == 0)
        {
            return Http3VarIntReadResult.Failure;
        }

        var length = 1 << (first[0] >> 6);
        var value = first[0] & 0x3f;
        if (length == 1)
        {
            return new Http3VarIntReadResult(true, value);
        }

        var rest = await ReadExactAsync(length - 1, timeout, timeoutKind, cancellationToken);
        foreach (var next in rest)
        {
            value = (value << 8) | next;
        }

        return new Http3VarIntReadResult(true, value);
    }

    private async ValueTask<byte[]> ReadExactAsync(
        int length,
        TimeSpan timeout,
        ProxyTimeoutKind timeoutKind,
        CancellationToken cancellationToken,
        bool allowEnd = false)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await ProxyTimeoutPolicy.RunAsync(
                async timeoutToken => await Stream.ReadAsync(buffer.AsMemory(offset, length - offset), timeoutToken),
                timeout,
                timeoutKind,
                cancellationToken);
            if (read == 0)
            {
                return allowEnd && offset == 0
                    ? []
                    : throw new Http3UpstreamProtocolException("Upstream closed mid HTTP/3 frame.");
            }

            _metrics.AddBytesRead(read);
            offset += read;
        }

        return buffer;
    }

    private async ValueTask WriteWithTimeoutAsync(
        ReadOnlyMemory<byte> bytes,
        bool completeWrites,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await ProxyTimeoutPolicy.RunAsync(
            async timeoutToken => await Stream.WriteAsync(bytes, completeWrites, timeoutToken),
            timeout,
            ProxyTimeoutKind.DownstreamWrite,
            cancellationToken);
        _metrics.AddBytesWritten(bytes.Length);
    }

    private static Http3UpstreamResponseHead DecodeResponseHeaders(
        ReadOnlySpan<byte> block,
        int maxHeaderListBytes)
    {
        if (!Http3PreviewCodec.TryDecodeHeaderBlock(block, maxHeaderListBytes, out var headers, out var reason))
        {
            throw new Http3UpstreamProtocolException($"Upstream sent invalid HTTP/3 response headers: {reason}.");
        }

        int? statusCode = null;
        List<Http1HeaderField> regularHeaders = [];
        foreach (var header in headers)
        {
            if (string.Equals(header.Name, ":status", StringComparison.Ordinal))
            {
                if (statusCode.HasValue
                    || !int.TryParse(header.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                    || parsed is < 100 or > 599)
                {
                    throw new Http3UpstreamProtocolException("Upstream sent an invalid HTTP/3 :status pseudo-header.");
                }

                statusCode = parsed;
                continue;
            }

            if (header.Name.StartsWith(":", StringComparison.Ordinal))
            {
                throw new Http3UpstreamProtocolException("Upstream sent an invalid HTTP/3 response pseudo-header.");
            }

            if (IsHopByHopHeader(header.Name))
            {
                throw new Http3UpstreamProtocolException("Upstream sent a forbidden HTTP/3 hop-by-hop response header.");
            }

            regularHeaders.Add(new Http1HeaderField(header.Name, header.Value));
        }

        if (!statusCode.HasValue)
        {
            throw new Http3UpstreamProtocolException("Upstream response did not include an HTTP/3 :status pseudo-header.");
        }

        return new Http3UpstreamResponseHead(statusCode.Value, regularHeaders);
    }

    private static async ValueTask<IPEndPoint> ResolveEndPointAsync(
        RuntimeUpstream upstream,
        CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(upstream.Address, out var address))
        {
            return new IPEndPoint(address, upstream.Port);
        }

        var addresses = await Dns.GetHostAddressesAsync(upstream.Address, cancellationToken);
        if (addresses.Length == 0)
        {
            throw new IOException($"Unable to resolve upstream '{upstream.Name}' at {upstream.Address}.");
        }

        return new IPEndPoint(addresses[0], upstream.Port);
    }

    private static bool IsHopByHopHeader(string name)
    {
        return string.Equals(name, "connection", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "keep-alive", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "proxy-connection", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "proxy-authenticate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "proxy-authorization", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "transfer-encoding", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "upgrade", StringComparison.OrdinalIgnoreCase);
    }

    private static async ValueTask DisposePartialConnectionAsync(
        QuicConnection? connection,
        QuicStream? stream,
        ProxyMetrics metrics,
        bool connectionOpened,
        bool streamStarted)
    {
        if (stream is not null)
        {
            await stream.DisposeAsync();
        }

        if (streamStarted)
        {
            metrics.UpstreamHttp3StreamEnded();
        }

        if (connection is not null)
        {
            await connection.DisposeAsync();
        }

        if (connectionOpened)
        {
            metrics.UpstreamHttp3ConnectionClosed();
            metrics.UpstreamHttp3PoolConnectionClosed();
        }
    }

    private readonly record struct Http3FrameReadResult(
        bool EndStream,
        long Type,
        ReadOnlyMemory<byte> Payload)
    {
        public static Http3FrameReadResult End { get; } = new(true, 0, ReadOnlyMemory<byte>.Empty);
    }

    private readonly record struct Http3VarIntReadResult(bool Success, long Value)
    {
        public static Http3VarIntReadResult Failure { get; } = new(false, 0);
    }
}

internal sealed record Http3UpstreamResponseHead(
    int StatusCode,
    IReadOnlyList<Http1HeaderField> Headers);

internal sealed record Http3UpstreamDataChunk(byte[] Data, bool EndStream);

internal sealed class Http3UpstreamProtocolException : IOException
{
    public Http3UpstreamProtocolException(string message)
        : base(message)
    {
    }

    public Http3UpstreamProtocolException(
        string message,
        Http3UpstreamFailureKind failureKind)
        : base(message)
    {
        FailureKind = failureKind;
    }

    public Http3UpstreamProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public Http3UpstreamProtocolException(
        string message,
        Http3UpstreamFailureKind failureKind,
        Exception innerException)
        : base(message, innerException)
    {
        FailureKind = failureKind;
    }

    public Http3UpstreamFailureKind FailureKind { get; } = Http3UpstreamFailureKind.ProtocolError;
}

internal enum Http3UpstreamFailureKind
{
    ProtocolError,
    ConnectFailure
}
