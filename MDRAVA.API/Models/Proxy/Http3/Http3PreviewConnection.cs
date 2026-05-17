#pragma warning disable CA1416
using System.Net;
using System.Net.Quic;
using System.Text;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Forwarding;
using MDRAVA.API.Proxy.Metrics;
using MDRAVA.API.Proxy.Observability;
using MDRAVA.API.Proxy.Routing;
using MDRAVA.API.Proxy.Runtime;

namespace MDRAVA.API.Proxy.Http3;

public sealed class Http3PreviewConnection
{
    private const int MaxFramePayloadBytes = 1024 * 1024;
    private readonly QuicConnection _connection;
    private readonly ProxyConfigurationSnapshot _configurationSnapshot;
    private readonly RuntimeListener _listener;
    private readonly IRouteMatcher _routeMatcher;
    private readonly ForwardedHeadersPolicy _forwardedHeadersPolicy;
    private readonly ProxyRouteActionPolicy _routeActionPolicy;
    private readonly ProxyMetrics _metrics;
    private readonly RequestIdGenerator _requestIdGenerator;
    private readonly AccessLogEmitter _accessLogEmitter;
    private readonly ClientRateLimiter _rateLimiter;
    private readonly ILogger _logger;

    public Http3PreviewConnection(
        QuicConnection connection,
        ProxyConfigurationSnapshot configurationSnapshot,
        RuntimeListener listener,
        IRouteMatcher routeMatcher,
        ForwardedHeadersPolicy forwardedHeadersPolicy,
        ProxyRouteActionPolicy routeActionPolicy,
        ProxyMetrics metrics,
        RequestIdGenerator requestIdGenerator,
        AccessLogEmitter accessLogEmitter,
        ClientRateLimiter rateLimiter,
        ILogger logger)
    {
        _connection = connection;
        _configurationSnapshot = configurationSnapshot;
        _listener = listener;
        _routeMatcher = routeMatcher;
        _forwardedHeadersPolicy = forwardedHeadersPolicy;
        _routeActionPolicy = routeActionPolicy;
        _metrics = metrics;
        _requestIdGenerator = requestIdGenerator;
        _accessLogEmitter = accessLogEmitter;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    public async ValueTask RunAsync(CancellationToken cancellationToken)
    {
        _metrics.Http3ConnectionAccepted();
        await using var ownedConnection = _connection;
        await SendSettingsAsync(cancellationToken);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var stream = await _connection.AcceptInboundStreamAsync(cancellationToken);
                if (stream.Type == QuicStreamType.Bidirectional)
                {
                    await ProcessRequestStreamAsync(stream, cancellationToken);
                }
                else
                {
                    await DrainUnsupportedStreamAsync(stream, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (QuicException exception)
        {
            _logger.LogDebug(exception, "HTTP/3 preview QUIC connection ended.");
        }
        catch (IOException exception)
        {
            _logger.LogDebug(exception, "HTTP/3 preview connection ended with I/O failure.");
        }
    }

    private async ValueTask ProcessRequestStreamAsync(
        QuicStream stream,
        CancellationToken cancellationToken)
    {
        await using var ownedStream = stream;
        var context = CreateRequestContext();
        try
        {
            var requestBytes = await ReadStreamBytesAsync(stream, _listener.Http2Limits.MaxHeaderListBytes + MaxFramePayloadBytes, cancellationToken);
            if (requestBytes is null)
            {
                await WriteGeneratedResponseAsync(stream, 413, "Payload Too Large", "Payload Too Large", context, "GET", cancellationToken);
                CompleteContext(ref context);
                return;
            }

            if (!TryReadHeaders(requestBytes, out var headers, out var rejectionReason))
            {
                _metrics.Http3ProtocolError(rejectionReason);
                await WriteGeneratedResponseAsync(stream, 400, "Bad Request", "Bad Request", context, "GET", cancellationToken);
                CompleteContext(ref context);
                return;
            }

            if (!Http3PreviewRequestTranslator.TryBuildRequest(headers, _listener, out var requestHead, out rejectionReason))
            {
                _metrics.Http3ProtocolError(rejectionReason);
                await WriteGeneratedResponseAsync(stream, 400, "Bad Request", "Bad Request", context, "GET", cancellationToken);
                CompleteContext(ref context);
                return;
            }

            _metrics.RequestReceived();
            _metrics.Http3RequestReceived();
            context.SetRequest(requestHead.Method, requestHead.Host, requestHead.Target, ExtractExternalRequestId(requestHead));

            if (!Http3PreviewRequestTranslator.IsSupportedPreviewMethod(requestHead.Method, out rejectionReason))
            {
                _metrics.Http3RequestRejected(rejectionReason);
                await WriteGeneratedResponseAsync(stream, 501, "Not Implemented", "Not Implemented", context, requestHead.Method, cancellationToken);
                CompleteContext(ref context);
                return;
            }

            var forwardedHeaders = _forwardedHeadersPolicy.Build(
                requestHead,
                _listener,
                _configurationSnapshot.ForwardedHeaders,
                _connection.RemoteEndPoint);
            context.SetClientEndpoint(forwardedHeaders.ResolvedClientEndpoint);

            if (!_rateLimiter.TryAcquireRequest(forwardedHeaders.ResolvedClientIp, _configurationSnapshot.Limits.RequestsPerMinutePerIp))
            {
                await WriteGeneratedResponseAsync(stream, 429, "Too Many Requests", "Too Many Requests", context, requestHead.Method, cancellationToken);
                CompleteContext(ref context);
                return;
            }

            var routeMatch = _routeMatcher.Match(_configurationSnapshot, requestHead);
            if (routeMatch is null)
            {
                await WriteGeneratedResponseAsync(stream, 404, "Not Found", "Not Found", context, requestHead.Method, cancellationToken);
                CompleteContext(ref context);
                return;
            }

            context.SetRoute(routeMatch.Route);
            var actionDecision = _routeActionPolicy.Evaluate(routeMatch.Route, requestHead, _listener, isUpgradeRequest: false);
            if (!actionDecision.ShouldProxy)
            {
                await WriteGeneratedRouteResponseAsync(stream, actionDecision.Response!, context, requestHead.Method, cancellationToken);
                CompleteContext(ref context);
                return;
            }

            _metrics.Http3RequestRejected("proxy_not_implemented");
            await WriteGeneratedResponseAsync(stream, 501, "Not Implemented", "HTTP/3 proxying is not implemented in preview.", context, requestHead.Method, cancellationToken);
            CompleteContext(ref context);
        }
        catch (Exception exception) when (exception is QuicException or IOException)
        {
            _metrics.Http3ProtocolError("io_error");
            _logger.LogDebug(exception, "HTTP/3 preview stream ended with I/O failure.");
            CompleteContext(ref context);
        }
    }

    private async ValueTask SendSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var control = await _connection.OpenOutboundStreamAsync(QuicStreamType.Unidirectional, cancellationToken);
            using var payload = new MemoryStream();
            Http3PreviewCodec.WriteVarInt(payload, Http3PreviewCodec.ControlStream);
            Http3PreviewCodec.WriteFrame(payload, Http3PreviewCodec.SettingsFrame, ReadOnlySpan<byte>.Empty);
            await control.WriteAsync(payload.ToArray(), completeWrites: true, cancellationToken);
        }
        catch (Exception exception) when (exception is QuicException or IOException)
        {
            _logger.LogDebug(exception, "HTTP/3 preview failed to send SETTINGS.");
        }
    }

    private async ValueTask DrainUnsupportedStreamAsync(QuicStream stream, CancellationToken cancellationToken)
    {
        await using var ownedStream = stream;
        var buffer = new byte[256];
        while (await stream.ReadAsync(buffer, cancellationToken) > 0)
        {
        }
    }

    private bool TryReadHeaders(
        byte[] requestBytes,
        out IReadOnlyList<Http1HeaderField> headers,
        out string rejectionReason)
    {
        headers = [];
        rejectionReason = "missing_headers";
        var offset = 0;
        var sawHeaders = false;
        while (offset < requestBytes.Length)
        {
            if (!Http3PreviewCodec.TryReadFrame(requestBytes, ref offset, out var frameType, out var payload))
            {
                rejectionReason = "invalid_frame";
                return false;
            }

            if (payload.Length > MaxFramePayloadBytes)
            {
                rejectionReason = "frame_too_large";
                return false;
            }

            if (frameType == Http3PreviewCodec.DataFrame && payload.Length > 0)
            {
                rejectionReason = "request_body_unsupported";
                return false;
            }

            if (frameType != Http3PreviewCodec.HeadersFrame)
            {
                continue;
            }

            if (sawHeaders)
            {
                rejectionReason = "duplicate_headers";
                return false;
            }

            sawHeaders = true;
            if (!Http3PreviewCodec.TryDecodeHeaderBlock(
                    payload.Span,
                    _listener.Http2Limits.MaxHeaderListBytes,
                    out headers,
                    out rejectionReason))
            {
                return false;
            }
        }

        return sawHeaders;
    }

    private async ValueTask<byte[]?> ReadStreamBytesAsync(
        QuicStream stream,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[4096];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return memory.ToArray();
            }

            if (memory.Length + read > maxBytes)
            {
                _metrics.Http3ProtocolError("stream_too_large");
                return null;
            }

            memory.Write(buffer, 0, read);
        }
    }

    private async ValueTask WriteGeneratedRouteResponseAsync(
        QuicStream stream,
        GeneratedRouteResponse response,
        ProxyRequestContext context,
        string method,
        CancellationToken cancellationToken)
    {
        await WriteGeneratedResponseAsync(
            stream,
            response.StatusCode,
            response.ReasonPhrase,
            response.Body,
            context,
            method,
            cancellationToken,
            response.ContentType,
            response.Headers);
    }

    private async ValueTask WriteGeneratedResponseAsync(
        QuicStream stream,
        int statusCode,
        string reasonPhrase,
        string body,
        ProxyRequestContext context,
        string method,
        CancellationToken cancellationToken,
        string? contentType = "text/plain; charset=utf-8",
        IReadOnlyList<Http1HeaderField>? extraHeaders = null)
    {
        _ = reasonPhrase;
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        List<Http1HeaderField> headers =
        [
            new(":status", Http3PreviewCodec.StatusText(statusCode)),
            new("x-request-id", context.RequestId),
            new("content-length", bodyBytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture))
        ];
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            headers.Add(new Http1HeaderField("content-type", contentType));
        }

        if (extraHeaders is not null)
        {
            foreach (var header in extraHeaders)
            {
                if (!IsHopByHopHeader(header.Name))
                {
                    headers.Add(new Http1HeaderField(header.Name.ToLowerInvariant(), header.Value));
                }
            }
        }

        var headerBlock = Http3PreviewCodec.EncodeHeaderBlock(headers);
        using var response = new MemoryStream();
        Http3PreviewCodec.WriteFrame(response, Http3PreviewCodec.HeadersFrame, headerBlock);
        if (!string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase) && bodyBytes.Length > 0)
        {
            Http3PreviewCodec.WriteFrame(response, Http3PreviewCodec.DataFrame, bodyBytes);
        }

        await stream.WriteAsync(response.ToArray(), completeWrites: true, cancellationToken);
        _metrics.AddBytesWritten(response.Length);
        context.ResponseStarted = true;
        context.ResponseStatusCode = statusCode;
        context.KeepClientConnectionOpen = false;
    }

    private ProxyRequestContext CreateRequestContext()
    {
        return new ProxyRequestContext(
            _requestIdGenerator.Create(),
            _listener.Name,
            _listener.Transport,
            _connection.RemoteEndPoint?.ToString(),
            _configurationSnapshot.Version);
    }

    private void CompleteContext(ref ProxyRequestContext? context)
    {
        if (context is null)
        {
            return;
        }

        _accessLogEmitter.Complete(
            context,
            context.AccessLogEnabled ?? _configurationSnapshot.Observability.AccessLogEnabled,
            _configurationSnapshot.Observability.RecentDiagnosticsCapacity);
        context = null;
    }

    private static bool IsHopByHopHeader(string header)
    {
        return string.Equals(header, "connection", StringComparison.OrdinalIgnoreCase)
            || string.Equals(header, "transfer-encoding", StringComparison.OrdinalIgnoreCase)
            || string.Equals(header, "upgrade", StringComparison.OrdinalIgnoreCase)
            || string.Equals(header, "keep-alive", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractExternalRequestId(Http1RequestHead requestHead)
    {
        foreach (var header in requestHead.Headers)
        {
            if (string.Equals(header.Name, "x-request-id", StringComparison.OrdinalIgnoreCase)
                && header.Value.Length is > 0 and <= 128
                && !header.Value.Any(static character => char.IsControl(character)))
            {
                return header.Value;
            }
        }

        return null;
    }
}
