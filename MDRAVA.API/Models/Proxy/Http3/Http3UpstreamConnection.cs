#pragma warning disable CA1416
using System.Globalization;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
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
    private readonly Http3UpstreamPooledConnection? _pooledConnection;
    private readonly QuicStream? _controlStream;
    private bool _connectionUsable = true;

    private Http3UpstreamConnection(
        QuicConnection connection,
        QuicStream stream,
        ProxyMetrics metrics,
        int maxFramePayloadBytes,
        QuicStream? controlStream,
        Http3UpstreamPooledConnection? pooledConnection)
    {
        Connection = connection;
        Stream = stream;
        _metrics = metrics;
        _maxFramePayloadBytes = Math.Clamp(maxFramePayloadBytes, 16 * 1024, MaxFramePayloadBytes);
        _controlStream = controlStream;
        _pooledConnection = pooledConnection;
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
            throw new Http3UpstreamProtocolException(
                "The current runtime does not support QUIC client connections.",
                Http3UpstreamFailureKind.ConnectFailure);
        }

        var remoteEndPoint = await ResolveEndPointAsync(upstream, cancellationToken);
        Http3UpstreamTransport? transport = null;
        QuicStream? stream = null;
        var streamStarted = false;
        try
        {
            transport = await OpenTransportAsync(upstream, remoteEndPoint, timeouts, metrics, cancellationToken);
            stream = await ProxyTimeoutPolicy.RunAsync(
                async timeoutToken => await transport.Connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, timeoutToken),
                timeouts.UpstreamConnectTimeout,
                ProxyTimeoutKind.UpstreamConnect,
                cancellationToken);
            metrics.UpstreamHttp3StreamStarted();
            streamStarted = true;
            return new Http3UpstreamConnection(transport.Connection, stream, metrics, maxFramePayloadBytes, transport.ControlStream, pooledConnection: null);
        }
        catch (Http3UpstreamProtocolException)
        {
            metrics.UpstreamHttp3ConnectionFailed();
            await DisposePartialConnectionAsync(transport, stream, metrics, streamStarted);

            throw;
        }
        catch (Exception exception) when (exception is AuthenticationException or IOException or QuicException)
        {
            metrics.UpstreamHttp3ConnectionFailed();
            await DisposePartialConnectionAsync(transport, stream, metrics, streamStarted);

            throw new Http3UpstreamProtocolException(
                "Failed to connect to the upstream HTTP/3 endpoint.",
                Http3UpstreamFailureKind.ConnectFailure,
                exception);
        }
    }

    internal static async ValueTask<Http3UpstreamTransport> OpenTransportAsync(
        RuntimeUpstream upstream,
        RuntimeTimeouts timeouts,
        ProxyMetrics metrics,
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
        return await OpenTransportAsync(upstream, remoteEndPoint, timeouts, metrics, cancellationToken);
    }

    internal static async ValueTask<Http3UpstreamConnection> OpenStreamAsync(
        Http3UpstreamPooledConnection pooledConnection,
        RuntimeTimeouts timeouts,
        ProxyMetrics metrics,
        int maxFramePayloadBytes,
        CancellationToken cancellationToken)
    {
        QuicStream? stream = null;
        var streamStarted = false;
        try
        {
            stream = await ProxyTimeoutPolicy.RunAsync(
                async timeoutToken => await pooledConnection.Connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, timeoutToken),
                timeouts.UpstreamConnectTimeout,
                ProxyTimeoutKind.UpstreamConnect,
                cancellationToken);
            metrics.UpstreamHttp3StreamStarted();
            streamStarted = true;
            return new Http3UpstreamConnection(pooledConnection.Connection, stream, metrics, maxFramePayloadBytes, controlStream: null, pooledConnection);
        }
        catch (Exception exception) when (exception is QuicException or IOException)
        {
            if (stream is not null)
            {
                await stream.DisposeAsync();
            }

            if (streamStarted)
            {
                metrics.UpstreamHttp3StreamEnded();
            }

            pooledConnection.MarkUnusable();
            pooledConnection.ReleaseStream(connectionUsable: false);
            throw new Http3UpstreamProtocolException(
                "Failed to open an upstream HTTP/3 request stream.",
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
        var block = Http3Codec.EncodeHeaderBlock(headers);
        using var memory = new MemoryStream();
        Http3Codec.WriteFrame(memory, Http3Codec.HeadersFrame, block);
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
            Http3Codec.WriteFrame(memory, Http3Codec.DataFrame, remaining[..chunkLength].Span);
            await WriteWithTimeoutAsync(memory.ToArray(), final, timeouts.DownstreamWriteTimeout, cancellationToken);
            remaining = remaining[chunkLength..];
        }

        if (body.Length == 0 && endStream)
        {
            using var memory = new MemoryStream();
            Http3Codec.WriteFrame(memory, Http3Codec.DataFrame, ReadOnlySpan<byte>.Empty);
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

            if (frame.Type == Http3Codec.DataFrame)
            {
                throw new Http3UpstreamProtocolException("Upstream sent HTTP/3 response DATA before response headers.");
            }

            if (frame.Type != Http3Codec.HeadersFrame)
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

            if (frame.Type == Http3Codec.DataFrame)
            {
                return new Http3UpstreamDataChunk(frame.Payload.ToArray(), EndStream: false);
            }

            if (frame.Type == Http3Codec.HeadersFrame)
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
            if (_pooledConnection is not null)
            {
                _pooledConnection.ReleaseStream(_connectionUsable);
            }
            else
            {
                try
                {
                    if (_controlStream is not null)
                    {
                        await _controlStream.DisposeAsync();
                    }

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
    }

    private static async ValueTask<Http3UpstreamTransport> OpenTransportAsync(
        RuntimeUpstream upstream,
        IPEndPoint remoteEndPoint,
        RuntimeTimeouts timeouts,
        ProxyMetrics metrics,
        CancellationToken cancellationToken)
    {
        QuicConnection? connection = null;
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
            var controlStream = await SendSettingsAsync(connection, timeouts, cancellationToken);
            return new Http3UpstreamTransport(connection, controlStream);
        }
        catch
        {
            if (connection is not null)
            {
                await connection.DisposeAsync();
                metrics.UpstreamHttp3ConnectionClosed();
                metrics.UpstreamHttp3PoolConnectionClosed();
            }

            throw;
        }
    }

    private static async ValueTask<QuicStream> SendSettingsAsync(
        QuicConnection connection,
        RuntimeTimeouts timeouts,
        CancellationToken cancellationToken)
    {
        var control = await ProxyTimeoutPolicy.RunAsync(
            async timeoutToken => await connection.OpenOutboundStreamAsync(QuicStreamType.Unidirectional, timeoutToken),
            timeouts.UpstreamConnectTimeout,
            ProxyTimeoutKind.UpstreamConnect,
            cancellationToken);
        using var payload = new MemoryStream();
        Http3Codec.WriteVarInt(payload, Http3Codec.ControlStream);
        using var settings = new MemoryStream();
        Http3Codec.WriteVarInt(settings, Http3Codec.QpackMaxTableCapacitySetting);
        Http3Codec.WriteVarInt(settings, 0);
        Http3Codec.WriteVarInt(settings, Http3Codec.QpackBlockedStreamsSetting);
        Http3Codec.WriteVarInt(settings, 0);
        Http3Codec.WriteFrame(payload, Http3Codec.SettingsFrame, settings.ToArray());
        await ProxyTimeoutPolicy.RunAsync(
            async timeoutToken => await control.WriteAsync(payload.ToArray(), completeWrites: false, timeoutToken),
            timeouts.DownstreamWriteTimeout,
            ProxyTimeoutKind.DownstreamWrite,
            cancellationToken);
        return control;
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
        if (!Http3Codec.TryDecodeHeaderBlock(block, maxHeaderListBytes, out var headers, out var reason))
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

            if (HopByHopHeaderPolicy.IsHopByHopHeader(header.Name))
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

    private static async ValueTask DisposePartialConnectionAsync(
        Http3UpstreamTransport? transport,
        QuicStream? stream,
        ProxyMetrics metrics,
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

        if (transport is not null)
        {
            try
            {
                await transport.ControlStream.DisposeAsync();
            }
            finally
            {
                await transport.Connection.DisposeAsync();
            }

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

internal sealed record Http3UpstreamTransport(
    QuicConnection Connection,
    QuicStream ControlStream);

internal sealed class Http3UpstreamPooledConnection : IAsyncDisposable
{
    private const int MaxControlFramePayloadBytes = 64 * 1024;

    private readonly object _gate = new();
    private readonly ProxyMetrics _metrics;
    private readonly CancellationTokenSource _controlMonitorStop = new();
    private readonly Task _controlMonitor;
    private Http3UpstreamPooledConnectionState _state = Http3UpstreamPooledConnectionState.Active;
    private int _activeStreams;

    public Http3UpstreamPooledConnection(
        string key,
        Http3UpstreamTransport transport,
        ProxyMetrics metrics,
        int maxConcurrentStreams)
    {
        Key = key;
        Connection = transport.Connection;
        ControlStream = transport.ControlStream;
        _metrics = metrics;
        MaxConcurrentStreams = Math.Clamp(maxConcurrentStreams, 1, 64);
        LastUsedUtc = DateTimeOffset.UtcNow;
        _controlMonitor = Task.Run(MonitorPeerStreamsAsync);
    }

    public string Key { get; }

    public QuicConnection Connection { get; }

    private QuicStream ControlStream { get; }

    public int MaxConcurrentStreams { get; }

    public DateTimeOffset LastUsedUtc { get; private set; }

    public Http3UpstreamPooledConnectionState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public bool TryReserveStream(TimeSpan idleLifetime)
    {
        lock (_gate)
        {
            if (_state != Http3UpstreamPooledConnectionState.Active
                || _activeStreams >= MaxConcurrentStreams)
            {
                return false;
            }

            if (_activeStreams == 0 && DateTimeOffset.UtcNow - LastUsedUtc > idleLifetime)
            {
                _state = Http3UpstreamPooledConnectionState.IdleExpired;
                return false;
            }

            _activeStreams++;
            return true;
        }
    }

    public bool ShouldPrune(TimeSpan idleLifetime)
    {
        lock (_gate)
        {
            if (_state is Http3UpstreamPooledConnectionState.Closed
                or Http3UpstreamPooledConnectionState.ShutdownDisposing)
            {
                return false;
            }

            if (_activeStreams > 0)
            {
                return false;
            }

            if (_state is Http3UpstreamPooledConnectionState.Draining
                or Http3UpstreamPooledConnectionState.Failed
                or Http3UpstreamPooledConnectionState.IdleExpired)
            {
                return true;
            }

            if (DateTimeOffset.UtcNow - LastUsedUtc > idleLifetime)
            {
                _state = Http3UpstreamPooledConnectionState.IdleExpired;
                return true;
            }

            return false;
        }
    }

    public void ReleaseStream(bool connectionUsable)
    {
        lock (_gate)
        {
            if (_activeStreams > 0)
            {
                _activeStreams--;
            }

            if (!connectionUsable)
            {
                _state = Http3UpstreamPooledConnectionState.Failed;
            }

            if (_activeStreams == 0)
            {
                LastUsedUtc = DateTimeOffset.UtcNow;
            }
        }
    }

    public void MarkUnusable()
    {
        lock (_gate)
        {
            if (_state is not Http3UpstreamPooledConnectionState.Closed
                and not Http3UpstreamPooledConnectionState.ShutdownDisposing)
            {
                _state = Http3UpstreamPooledConnectionState.Failed;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_state is Http3UpstreamPooledConnectionState.Closed
                or Http3UpstreamPooledConnectionState.ShutdownDisposing)
            {
                return;
            }

            _state = Http3UpstreamPooledConnectionState.ShutdownDisposing;
        }

        _controlMonitorStop.Cancel();
        try
        {
            await ControlStream.DisposeAsync();
            await Connection.CloseAsync(0, CancellationToken.None);
        }
        catch (Exception exception) when (exception is QuicException or IOException or ObjectDisposedException)
        {
        }
        finally
        {
            await Connection.DisposeAsync();
            try
            {
                await _controlMonitor.WaitAsync(TimeSpan.FromMilliseconds(250));
            }
            catch (Exception exception) when (exception is OperationCanceledException or TimeoutException or QuicException or IOException)
            {
            }

            _controlMonitorStop.Dispose();
            _metrics.UpstreamHttp3ConnectionClosed();
            _metrics.UpstreamHttp3PoolConnectionClosed();
            lock (_gate)
            {
                _state = Http3UpstreamPooledConnectionState.Closed;
            }
        }
    }

    private async Task MonitorPeerStreamsAsync()
    {
        var cancellationToken = _controlMonitorStop.Token;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var stream = await Connection.AcceptInboundStreamAsync(cancellationToken);
                _ = Task.Run(
                    async () => await ProcessInboundStreamAsync(stream, cancellationToken),
                    CancellationToken.None);
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException)
        {
        }
        catch (Exception exception) when (exception is QuicException or IOException)
        {
            MarkUnusableUnlessDisposing();
        }
    }

    private async Task ProcessInboundStreamAsync(
        QuicStream stream,
        CancellationToken cancellationToken)
    {
        await using var ownedStream = stream;
        try
        {
            if (stream.Type != QuicStreamType.Unidirectional)
            {
                await DrainControlStreamAsync(stream, cancellationToken);
                return;
            }

            var streamType = await ReadControlVarIntAsync(stream, cancellationToken, allowEnd: true);
            if (!streamType.Success)
            {
                return;
            }

            if (streamType.Value != Http3Codec.ControlStream)
            {
                await DrainControlStreamAsync(stream, cancellationToken);
                return;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                var frameType = await ReadControlVarIntAsync(stream, cancellationToken, allowEnd: true);
                if (!frameType.Success)
                {
                    return;
                }

                var length = await ReadControlVarIntAsync(stream, cancellationToken, allowEnd: false);
                if (!length.Success
                    || length.Value < 0
                    || length.Value > MaxControlFramePayloadBytes)
                {
                    MarkUnusable();
                    _metrics.UpstreamHttp3ProtocolError("peer_control_malformed");
                    return;
                }

                var payload = length.Value == 0
                    ? []
                    : await ReadExactControlAsync(stream, (int)length.Value, cancellationToken);
                if (frameType.Value == Http3Codec.GoAwayFrame)
                {
                    HandleGoAway(payload);
                    continue;
                }

                if (frameType.Value == Http3Codec.SettingsFrame)
                {
                    continue;
                }
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException)
        {
        }
        catch (Exception exception) when (exception is QuicException or IOException)
        {
            MarkUnusableUnlessDisposing();
        }
    }

    private void HandleGoAway(byte[] payload)
    {
        var offset = 0;
        if (payload.Length > 0)
        {
            if (!Http3Codec.TryReadVarInt(payload, ref offset, out var decoded)
                || offset != payload.Length)
            {
                MarkUnusable();
                _metrics.UpstreamHttp3ProtocolError("goaway_malformed");
                return;
            }
        }

        lock (_gate)
        {
            if (_state == Http3UpstreamPooledConnectionState.Active)
            {
                _state = Http3UpstreamPooledConnectionState.Draining;
            }
        }
    }

    private void MarkUnusableUnlessDisposing()
    {
        lock (_gate)
        {
            if (_state is Http3UpstreamPooledConnectionState.ShutdownDisposing
                or Http3UpstreamPooledConnectionState.Closed)
            {
                return;
            }

            _state = Http3UpstreamPooledConnectionState.Failed;
        }
    }

    private static async ValueTask DrainControlStreamAsync(
        QuicStream stream,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[512];
        while (await stream.ReadAsync(buffer, cancellationToken) > 0)
        {
        }
    }

    private static async ValueTask<Http3ControlVarIntReadResult> ReadControlVarIntAsync(
        QuicStream stream,
        CancellationToken cancellationToken,
        bool allowEnd)
    {
        var first = await ReadExactControlAsync(stream, 1, cancellationToken, allowEnd);
        if (first.Length == 0)
        {
            return Http3ControlVarIntReadResult.Failure;
        }

        var length = 1 << (first[0] >> 6);
        var value = first[0] & 0x3f;
        if (length == 1)
        {
            return new Http3ControlVarIntReadResult(true, value);
        }

        var rest = await ReadExactControlAsync(stream, length - 1, cancellationToken, allowEnd: false);
        foreach (var next in rest)
        {
            value = (value << 8) | next;
        }

        return new Http3ControlVarIntReadResult(true, value);
    }

    private static async ValueTask<byte[]> ReadExactControlAsync(
        QuicStream stream,
        int length,
        CancellationToken cancellationToken,
        bool allowEnd = false)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken);
            if (read == 0)
            {
                return allowEnd && offset == 0
                    ? []
                    : throw new IOException("Peer HTTP/3 control stream closed mid frame.");
            }

            offset += read;
        }

        return buffer;
    }

    private readonly record struct Http3ControlVarIntReadResult(bool Success, long Value)
    {
        public static Http3ControlVarIntReadResult Failure { get; } = new(false, 0);
    }
}

internal enum Http3UpstreamPooledConnectionState
{
    Active,
    Draining,
    Failed,
    IdleExpired,
    ShutdownDisposing,
    Closed
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
