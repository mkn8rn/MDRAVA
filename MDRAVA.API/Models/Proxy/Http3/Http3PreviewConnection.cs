#pragma warning disable CA1416
using System.Net;
using System.Net.Quic;
using System.Text;
using MDRAVA.API.Proxy.Acme;
using MDRAVA.API.Proxy.Caching;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Forwarding;
using MDRAVA.API.Proxy.Health;
using MDRAVA.API.Proxy.Metrics;
using MDRAVA.API.Proxy.Observability;
using MDRAVA.API.Proxy.Protocol;
using MDRAVA.API.Proxy.Resilience;
using MDRAVA.API.Proxy.Routing;
using MDRAVA.API.Proxy.Runtime;

namespace MDRAVA.API.Proxy.Http3;

public sealed class Http3PreviewConnection
{
    private const int MaxFramePayloadBytes = 1024 * 1024;
    private const int MaxProtocolErrorsPerConnection = 8;
    private readonly QuicConnection _connection;
    private readonly ProxyConfigurationSnapshot _configurationSnapshot;
    private readonly RuntimeListener _listener;
    private readonly IRouteMatcher _routeMatcher;
    private readonly IUpstreamSelector _upstreamSelector;
    private readonly UpstreamHealthStore _healthStore;
    private readonly ProxyForwarder _forwarder;
    private readonly ForwardedHeadersPolicy _forwardedHeadersPolicy;
    private readonly ProxyRouteActionPolicy _routeActionPolicy;
    private readonly PathRewritePolicy _pathRewritePolicy;
    private readonly ResponseCacheStore _cacheStore;
    private readonly CircuitBreakerStore _circuitBreakerStore;
    private readonly AcmeHttp01ChallengeResponder _acmeChallengeResponder;
    private readonly ProxyMetrics _metrics;
    private readonly RequestIdGenerator _requestIdGenerator;
    private readonly AccessLogEmitter _accessLogEmitter;
    private readonly ClientRateLimiter _rateLimiter;
    private readonly ILogger _logger;
    private int _protocolErrors;

    public Http3PreviewConnection(
        QuicConnection connection,
        ProxyConfigurationSnapshot configurationSnapshot,
        RuntimeListener listener,
        IRouteMatcher routeMatcher,
        IUpstreamSelector upstreamSelector,
        UpstreamHealthStore healthStore,
        ProxyForwarder forwarder,
        ForwardedHeadersPolicy forwardedHeadersPolicy,
        ProxyRouteActionPolicy routeActionPolicy,
        PathRewritePolicy pathRewritePolicy,
        ResponseCacheStore cacheStore,
        CircuitBreakerStore circuitBreakerStore,
        AcmeHttp01ChallengeResponder acmeChallengeResponder,
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
        _upstreamSelector = upstreamSelector;
        _healthStore = healthStore;
        _forwarder = forwarder;
        _forwardedHeadersPolicy = forwardedHeadersPolicy;
        _routeActionPolicy = routeActionPolicy;
        _pathRewritePolicy = pathRewritePolicy;
        _cacheStore = cacheStore;
        _circuitBreakerStore = circuitBreakerStore;
        _acmeChallengeResponder = acmeChallengeResponder;
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
                    if (!await ProcessRequestStreamAsync(stream, cancellationToken))
                    {
                        await _connection.CloseAsync(0x100, CancellationToken.None);
                        return;
                    }
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

    private async ValueTask<bool> ProcessRequestStreamAsync(
        QuicStream stream,
        CancellationToken cancellationToken)
    {
        await using var ownedStream = stream;
        var context = CreateRequestContext();
        _metrics.Http3StreamStarted();
        try
        {
            var maxBufferedBodyBytes = EffectiveMaxBufferedBodyBytes(_listener, _configurationSnapshot.Limits);
            var requestBytes = await ReadStreamBytesAsync(
                stream,
                MaxRequestStreamBytes(_listener.Http2Limits.MaxHeaderListBytes, maxBufferedBodyBytes),
                cancellationToken);
            if (requestBytes is null)
            {
                _metrics.RequestBodySizeRejected();
                _metrics.Http3RequestRejected("request_body_too_large");
                var closeConnection = RecordProtocolError("stream_too_large");
                await WriteGeneratedResponseAsync(stream, 413, "Payload Too Large", "Payload Too Large", context, "GET", cancellationToken);
                CompleteContext(ref context);
                return !closeConnection;
            }

            if (!TryReadRequestFrames(requestBytes, maxBufferedBodyBytes, out var headers, out var requestBody, out var rejectionReason))
            {
                if (string.Equals(rejectionReason, "request_body_too_large", StringComparison.Ordinal))
                {
                    _metrics.RequestBodySizeRejected();
                    _metrics.Http3RequestRejected(rejectionReason);
                }

                var closeConnection = RecordProtocolError(rejectionReason);
                var statusCode = string.Equals(rejectionReason, "request_body_too_large", StringComparison.Ordinal)
                    ? 413
                    : 400;
                var reasonPhrase = statusCode == 413 ? "Payload Too Large" : "Bad Request";
                await WriteGeneratedResponseAsync(stream, statusCode, reasonPhrase, reasonPhrase, context, "GET", cancellationToken);
                CompleteContext(ref context);
                return !closeConnection;
            }

            if (!Http3PreviewRequestTranslator.TryBuildRequest(
                    headers,
                    _listener,
                    out var requestHead,
                    out rejectionReason,
                    requestBody.Length))
            {
                var closeConnection = RecordProtocolError(rejectionReason);
                await WriteGeneratedResponseAsync(stream, 400, "Bad Request", "Bad Request", context, "GET", cancellationToken);
                CompleteContext(ref context);
                return !closeConnection;
            }

            _metrics.RequestReceived();
            _metrics.Http3RequestReceived();
            context.SetRequest(requestHead.Method, requestHead.Host, requestHead.Target, ExtractExternalRequestId(requestHead));

            if (!Http3PreviewRequestTranslator.IsSupportedPreviewMethod(requestHead.Method, out rejectionReason))
            {
                _metrics.Http3RequestRejected(rejectionReason);
                await WriteGeneratedResponseAsync(stream, 501, "Not Implemented", "Not Implemented", context, requestHead.Method, cancellationToken);
                CompleteContext(ref context);
                return true;
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
                return true;
            }

            if (_acmeChallengeResponder.TryCreateResponse(requestHead, out var acmeChallengeResponse))
            {
                _metrics.Http3GeneratedResponse();
                await WriteGeneratedRouteResponseAsync(stream, acmeChallengeResponse, context, requestHead.Method, cancellationToken);
                CompleteContext(ref context);
                return true;
            }

            var routeMatch = _routeMatcher.Match(_configurationSnapshot, requestHead);
            if (routeMatch is null)
            {
                await WriteGeneratedResponseAsync(stream, 404, "Not Found", "Not Found", context, requestHead.Method, cancellationToken);
                CompleteContext(ref context);
                return true;
            }

            context.SetRoute(routeMatch.Route);
            var actionDecision = _routeActionPolicy.Evaluate(routeMatch.Route, requestHead, _listener, isUpgradeRequest: false);
            if (!actionDecision.ShouldProxy)
            {
                _metrics.Http3GeneratedResponse();
                await WriteGeneratedRouteResponseAsync(stream, actionDecision.Response!, context, requestHead.Method, cancellationToken);
                CompleteContext(ref context);
                return true;
            }

            if (requestHead.Framing.Kind == Http1BodyKind.ContentLength
                && requestHead.Framing.ContentLength.GetValueOrDefault() > routeMatch.Route.ResolvedOptions.MaxRequestBodyBytes)
            {
                _metrics.RequestBodySizeRejected();
                _metrics.Http3RequestRejected("request_body_too_large");
                await WriteGeneratedResponseAsync(stream, 413, "Payload Too Large", "Payload Too Large", context, requestHead.Method, cancellationToken);
                CompleteContext(ref context);
                return true;
            }

            var upstreamTarget = _pathRewritePolicy.Apply(routeMatch.Route, requestHead.Target, requestHead.Path);
            var effectiveTimeouts = ApplyRouteTimeouts(routeMatch.Route, _configurationSnapshot.Timeouts);
            if (_cacheStore.TryGet(
                    routeMatch.Route,
                    _listener,
                    requestHead,
                    upstreamTarget,
                    out var cachedResponse)
                && cachedResponse is not null)
            {
                await WriteCachedResponseAsync(stream, requestHead, cachedResponse, context, cancellationToken);
                CompleteContext(ref context);
                return true;
            }

            _metrics.Http3ProxiedRequest();
            var result = await ForwardWithRetriesAsync(
                stream,
                requestBody,
                requestHead,
                routeMatch.Route,
                effectiveTimeouts,
                _configurationSnapshot.ConnectionLimits,
                _configurationSnapshot.Limits,
                upstreamTarget,
                forwardedHeaders,
                context,
                context.RequestId,
                cancellationToken);
            ApplyForwardingResult(context, result);
            CompleteContext(ref context);
            return true;
        }
        catch (Exception exception) when (exception is QuicException or IOException)
        {
            _metrics.Http3ProtocolError("io_error");
            if (exception is QuicException)
            {
                _metrics.Http3StreamReset();
            }

            _logger.LogDebug(exception, "HTTP/3 preview stream ended with I/O failure.");
            CompleteContext(ref context);
            return true;
        }
        finally
        {
            _metrics.Http3StreamEnded();
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

    private bool TryReadRequestFrames(
        byte[] requestBytes,
        int maxBodyBytes,
        out IReadOnlyList<Http1HeaderField> headers,
        out byte[] body,
        out string rejectionReason)
    {
        headers = [];
        body = [];
        rejectionReason = "missing_headers";
        var offset = 0;
        var sawHeaders = false;
        using var bodyBuffer = new MemoryStream();
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

            if (frameType == Http3PreviewCodec.DataFrame)
            {
                if (!sawHeaders)
                {
                    rejectionReason = "unexpected_data";
                    return false;
                }

                if (bodyBuffer.Length + payload.Length > maxBodyBytes)
                {
                    rejectionReason = "request_body_too_large";
                    return false;
                }

                bodyBuffer.Write(payload.Span);
                _metrics.AddHttp3RequestBodyBytesReceived(payload.Length);
                continue;
            }

            if (frameType != Http3PreviewCodec.HeadersFrame)
            {
                rejectionReason = "unsupported_frame";
                return false;
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

        body = bodyBuffer.ToArray();
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
                return null;
            }

            memory.Write(buffer, 0, read);
            _metrics.AddBytesRead(read);
        }
    }

    private static int EffectiveMaxBufferedBodyBytes(RuntimeListener listener, RuntimeLimits limits)
    {
        if (limits.MaxRequestBodyBytes <= 0 || listener.Http3MaxBufferedRequestBodyBytes <= 0)
        {
            return 0;
        }

        return (int)Math.Min(limits.MaxRequestBodyBytes, listener.Http3MaxBufferedRequestBodyBytes);
    }

    private static int MaxRequestStreamBytes(int maxHeaderBytes, int maxBodyBytes)
    {
        return Math.Min(
            int.MaxValue,
            Math.Max(0, maxHeaderBytes) + maxBodyBytes + 64 * 1024);
    }

    private async ValueTask<ForwardingResult> ForwardWithRetriesAsync(
        QuicStream stream,
        byte[] body,
        Http1RequestHead requestHead,
        RuntimeRoute route,
        RuntimeTimeouts timeouts,
        RuntimeConnectionLimits connectionLimits,
        RuntimeLimits limits,
        string upstreamTarget,
        ForwardedHeadersContext forwardedHeaders,
        ProxyRequestContext context,
        string requestId,
        CancellationToken cancellationToken)
    {
        var retryAllowed = IsRetryAllowed(route, requestHead, out var skipReason);
        if (!retryAllowed && skipReason is not null)
        {
            _metrics.RetrySkipped(skipReason);
        }

        var maxAttempts = retryAllowed ? route.Retry.MaxAttempts : 1;
        ForwardingResult? lastResult = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var selection = _upstreamSelector.Select(route);
            if (selection is null)
            {
                if (attempt > 1)
                {
                    _metrics.RetryExhausted();
                }

                await WriteGeneratedResponseAsync(
                    stream,
                    503,
                    "Service Unavailable",
                    "Service Unavailable",
                    context,
                    requestHead.Method,
                    cancellationToken);
                return new ForwardingResult(false, true, false, 503, ProxyFailureKind.NoHealthyUpstream);
            }

            context.SetUpstream(selection.Upstream);
            var suppressGeneratedFailureResponse = retryAllowed && attempt < maxAttempts;
            var translator = new Http3ResponseTranslationStream(
                this,
                stream,
                requestHead.Method,
                _configurationSnapshot.Timeouts.DownstreamWriteTimeout,
                body);
            var result = await _forwarder.ForwardAsync(
                translator,
                new Http1HeadReadResult(0, 0, ReadOnlyMemory<byte>.Empty, body),
                requestHead,
                route,
                selection.Upstream,
                _listener,
                ApplyRetryAttemptTimeout(route, timeouts),
                connectionLimits,
                limits,
                upstreamTarget,
                forwardedHeaders,
                preferClientKeepAlive: false,
                requestId,
                cancellationToken,
                suppressGeneratedFailureResponse);
            lastResult = result;
            RecordUpstreamAttemptResult(selection, result);

            string? finalSkipReason = null;
            if (retryAllowed
                && ShouldRetry(route.Retry, requestHead, result, attempt, maxAttempts, out finalSkipReason))
            {
                _metrics.RetryAttempted();
                if (route.Retry.RetryBackoff > TimeSpan.Zero)
                {
                    await Task.Delay(route.Retry.RetryBackoff, cancellationToken);
                }

                continue;
            }

            if (finalSkipReason is not null)
            {
                _metrics.RetrySkipped(finalSkipReason);
            }

            if (retryAllowed && attempt == maxAttempts && IsRetryableFailure(route.Retry, result))
            {
                _metrics.RetryExhausted();
            }

            if (suppressGeneratedFailureResponse && !result.Succeeded && !result.ResponseStarted)
            {
                return await WriteSuppressedFailureAsync(stream, result, context, requestHead.Method, cancellationToken);
            }

            await translator.CompleteAsync(cancellationToken);
            return result;
        }

        if (lastResult is not null && !lastResult.ResponseStarted)
        {
            return await WriteSuppressedFailureAsync(stream, lastResult, context, requestHead.Method, cancellationToken);
        }

        return lastResult ?? new ForwardingResult(false, false, false, null, ProxyFailureKind.NoHealthyUpstream);
    }

    private async ValueTask<ForwardingResult> WriteSuppressedFailureAsync(
        QuicStream stream,
        ForwardingResult result,
        ProxyRequestContext context,
        string method,
        CancellationToken cancellationToken)
    {
        var statusCode = result.ResponseStatusCode ?? StatusCodeForFailure(result.FailureKind);
        if (statusCode == 502)
        {
            _metrics.ProxyGenerated502();
        }
        else if (statusCode == 504)
        {
            _metrics.ProxyGenerated504();
        }

        var reason = ProxyRouteActionPolicy.ReasonPhrase(statusCode);
        await WriteGeneratedResponseAsync(
            stream,
            statusCode,
            reason,
            reason,
            context,
            method,
            cancellationToken);
        return result with
        {
            ResponseStarted = true,
            KeepClientConnectionOpen = false,
            ResponseStatusCode = statusCode
        };
    }

    private void RecordUpstreamAttemptResult(UpstreamSelection selection, ForwardingResult result)
    {
        if (result.ResponseStatusCode.HasValue
            && selection.Upstream.CircuitBreaker.FailureStatusCodes.Any(code => code == result.ResponseStatusCode.Value))
        {
            _circuitBreakerStore.RecordFailure(selection.CircuitBreakerLease, "status_code", result.ResponseStatusCode);
            return;
        }

        if (!result.Succeeded)
        {
            _healthStore.RecordRequestFailure(selection.Upstream);
            if (IsCircuitFailure(result.FailureKind))
            {
                _circuitBreakerStore.RecordFailure(selection.CircuitBreakerLease, CircuitFailureReason(result.FailureKind));
            }
            else
            {
                selection.CircuitBreakerLease?.Dispose();
            }

            return;
        }

        _circuitBreakerStore.RecordSuccess(selection.CircuitBreakerLease);
    }

    private async ValueTask WriteCachedResponseAsync(
        QuicStream stream,
        Http1RequestHead requestHead,
        CachedProxyResponse response,
        ProxyRequestContext context,
        CancellationToken cancellationToken)
    {
        var ageSeconds = Math.Max(
            0,
            (long)Math.Floor((DateTimeOffset.UtcNow - response.StoredAtUtc).TotalSeconds));
        var headers = response.Headers
            .Where(static header => !IsHopByHopHeader(header.Name))
            .Append(new Http1HeaderField("age", ageSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .Append(new Http1HeaderField("x-request-id", context.RequestId))
            .Append(new Http1HeaderField("content-length", response.Body.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .ToArray();
        var includeBody = !string.Equals(requestHead.Method, "HEAD", StringComparison.OrdinalIgnoreCase);
        await WriteHeadersAndBodyAsync(stream, response.StatusCode, headers, includeBody ? response.Body : [], cancellationToken);
        context.ResponseStarted = true;
        context.ResponseStatusCode = response.StatusCode;
        context.KeepClientConnectionOpen = true;
        context.SetRouteAction("cache");
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

        await WriteHeadersAndBodyAsync(
            stream,
            statusCode,
            headers,
            string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase) ? [] : bodyBytes,
            cancellationToken);
        context.ResponseStarted = true;
        context.ResponseStatusCode = statusCode;
        context.KeepClientConnectionOpen = true;
    }

    private async ValueTask WriteHeadersAndBodyAsync(
        QuicStream stream,
        int statusCode,
        IReadOnlyList<Http1HeaderField> headers,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        await WriteHeadersAsync(
            stream,
            statusCode,
            headers,
            completeWrites: body.Length == 0,
            cancellationToken);
        if (body.Length > 0)
        {
            await WriteDataAsync(stream, body, completeWrites: true, cancellationToken);
        }
    }

    private async ValueTask WriteHeadersAsync(
        QuicStream stream,
        int statusCode,
        IReadOnlyList<Http1HeaderField> headers,
        bool completeWrites,
        CancellationToken cancellationToken)
    {
        List<Http1HeaderField> encodedHeaders = [new(":status", Http3PreviewCodec.StatusText(statusCode))];
        foreach (var header in headers)
        {
            if (!header.Name.StartsWith(':') && !IsHopByHopHeader(header.Name))
            {
                encodedHeaders.Add(new Http1HeaderField(header.Name.ToLowerInvariant(), header.Value));
            }
        }

        var headerBlock = Http3PreviewCodec.EncodeHeaderBlock(encodedHeaders);
        await WriteFrameAsync(stream, Http3PreviewCodec.HeadersFrame, headerBlock, completeWrites, cancellationToken);
    }

    private async ValueTask WriteDataAsync(
        QuicStream stream,
        ReadOnlyMemory<byte> body,
        bool completeWrites,
        CancellationToken cancellationToken)
    {
        var remaining = body;
        while (remaining.Length > 0)
        {
            var chunkLength = Math.Min(_listener.Http2Limits.MaxFrameSize, remaining.Length);
            var final = completeWrites && chunkLength == remaining.Length;
            await WriteFrameAsync(
                stream,
                Http3PreviewCodec.DataFrame,
                remaining[..chunkLength],
                final,
                cancellationToken);
            _metrics.AddHttp3ResponseBytesSent(chunkLength);
            remaining = remaining[chunkLength..];
        }

        if (body.Length == 0 && completeWrites)
        {
            await WriteFrameAsync(stream, Http3PreviewCodec.DataFrame, ReadOnlyMemory<byte>.Empty, completeWrites: true, cancellationToken);
        }
    }

    private async ValueTask WriteFrameAsync(
        QuicStream stream,
        long frameType,
        ReadOnlyMemory<byte> payload,
        bool completeWrites,
        CancellationToken cancellationToken)
    {
        using var frame = new MemoryStream();
        Http3PreviewCodec.WriteFrame(frame, frameType, payload.Span);
        await stream.WriteAsync(frame.ToArray(), completeWrites, cancellationToken);
        _metrics.AddBytesWritten(frame.Length);
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

    private bool RecordProtocolError(string reason)
    {
        _metrics.Http3ProtocolError(reason);
        return Interlocked.Increment(ref _protocolErrors) >= MaxProtocolErrorsPerConnection;
    }

    private static bool IsRetryAllowed(RuntimeRoute route, Http1RequestHead requestHead, out string? skipReason)
    {
        skipReason = null;
        if (!route.Retry.Enabled)
        {
            return false;
        }

        if (!route.Retry.RetryMethods.Any(method => string.Equals(method, requestHead.Method, StringComparison.OrdinalIgnoreCase)))
        {
            skipReason = "method";
            return false;
        }

        if (requestHead.Framing.Kind != Http1BodyKind.None)
        {
            skipReason = "request_body";
            return false;
        }

        return true;
    }

    private static bool ShouldRetry(
        RuntimeRetryPolicy retry,
        Http1RequestHead requestHead,
        ForwardingResult result,
        int attempt,
        int maxAttempts,
        out string? skipReason)
    {
        _ = requestHead;
        skipReason = null;
        if (!IsRetryableFailure(retry, result))
        {
            return false;
        }

        if (result.ResponseStarted)
        {
            skipReason = "response_started";
            return false;
        }

        return attempt < maxAttempts;
    }

    private static bool IsRetryableFailure(RuntimeRetryPolicy retry, ForwardingResult result)
    {
        if (result.ResponseStatusCode.HasValue
            && retry.RetryOnStatusCodes.Any(code => code == result.ResponseStatusCode.Value))
        {
            return true;
        }

        if (!result.Succeeded)
        {
            return result.FailureKind switch
            {
                ProxyFailureKind.UpstreamConnectFailed => retry.RetryOnConnectFailure,
                ProxyFailureKind.UpstreamConnectTimeout => retry.RetryOnConnectFailure,
                ProxyFailureKind.UpstreamResponseHeadTimeout => retry.RetryOnUpstreamResponseHeadTimeout,
                _ => false
            };
        }

        return false;
    }

    private static bool IsCircuitFailure(ProxyFailureKind failureKind)
    {
        return failureKind is ProxyFailureKind.UpstreamConnectFailed
            or ProxyFailureKind.UpstreamConnectTimeout
            or ProxyFailureKind.UpstreamResponseHeadTimeout;
    }

    private static string CircuitFailureReason(ProxyFailureKind failureKind)
    {
        return failureKind switch
        {
            ProxyFailureKind.UpstreamConnectFailed => "connect_failure",
            ProxyFailureKind.UpstreamConnectTimeout => "connect_timeout",
            ProxyFailureKind.UpstreamResponseHeadTimeout => "response_head_timeout",
            _ => "other"
        };
    }

    private static int StatusCodeForFailure(ProxyFailureKind failureKind)
    {
        return failureKind switch
        {
            ProxyFailureKind.UpstreamConnectTimeout => 504,
            ProxyFailureKind.UpstreamResponseHeadTimeout => 504,
            ProxyFailureKind.RequestPayloadTooLarge => 413,
            ProxyFailureKind.ClientMalformedRequest => 400,
            _ => 502
        };
    }

    private static RuntimeTimeouts ApplyRouteTimeouts(RuntimeRoute route, RuntimeTimeouts timeouts)
    {
        return timeouts with
        {
            UpstreamResponseHeadTimeout = route.ResolvedOptions.UpstreamResponseHeadTimeout
        };
    }

    private static RuntimeTimeouts ApplyRetryAttemptTimeout(RuntimeRoute route, RuntimeTimeouts timeouts)
    {
        if (route.Retry.PerAttemptTimeout is not { } perAttemptTimeout)
        {
            return timeouts;
        }

        return timeouts with
        {
            UpstreamConnectTimeout = perAttemptTimeout,
            UpstreamResponseHeadTimeout = perAttemptTimeout
        };
    }

    private static void ApplyForwardingResult(ProxyRequestContext context, ForwardingResult result)
    {
        context.ResponseStarted = result.ResponseStarted;
        context.ResponseStatusCode = result.ResponseStatusCode;
        context.KeepClientConnectionOpen = true;
        context.FailureKind = result.FailureKind;
    }

    private static bool IsHopByHopHeader(string header)
    {
        return string.Equals(header, "connection", StringComparison.OrdinalIgnoreCase)
            || string.Equals(header, "transfer-encoding", StringComparison.OrdinalIgnoreCase)
            || string.Equals(header, "upgrade", StringComparison.OrdinalIgnoreCase)
            || string.Equals(header, "keep-alive", StringComparison.OrdinalIgnoreCase)
            || string.Equals(header, "proxy-connection", StringComparison.OrdinalIgnoreCase)
            || string.Equals(header, "proxy-authenticate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(header, "proxy-authorization", StringComparison.OrdinalIgnoreCase);
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

    private sealed class Http3ResponseTranslationStream : Stream
    {
        private readonly Http3PreviewConnection _connection;
        private readonly QuicStream _stream;
        private readonly string _method;
        private readonly TimeSpan _writeTimeout;
        private readonly MemoryStream _requestBody;
        private readonly MemoryStream _headBuffer = new();
        private readonly MemoryStream _chunkBuffer = new();
        private bool _headWritten;
        private bool _endStreamSent;
        private bool _dropBody;
        private bool _decodeChunkedBody;
        private bool _responseStreamActive;
        private ChunkParserState _chunkState = ChunkParserState.ReadingSize;
        private long _chunkBytesRemaining;

        public Http3ResponseTranslationStream(
            Http3PreviewConnection connection,
            QuicStream stream,
            string method,
            TimeSpan writeTimeout,
            byte[] requestBody)
        {
            _connection = connection;
            _stream = stream;
            _method = method;
            _writeTimeout = writeTimeout;
            _requestBody = new MemoryStream(requestBody, writable: false);
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _requestBody.Read(buffer, offset, count);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return _requestBody.ReadAsync(buffer, cancellationToken);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            try
            {
                await ProxyTimeoutPolicy.RunAsync(
                    async timeoutToken => await WriteCoreAsync(buffer, timeoutToken),
                    _writeTimeout,
                    ProxyTimeoutKind.DownstreamWrite,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is QuicException or IOException)
            {
                _connection._metrics.Http3ResponseStreamReset();
                EndResponseStream();
                throw;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        }

        public async ValueTask CompleteAsync(CancellationToken cancellationToken)
        {
            if (_headWritten && !_endStreamSent)
            {
                await _connection.WriteDataAsync(_stream, ReadOnlyMemory<byte>.Empty, completeWrites: true, cancellationToken);
                _endStreamSent = true;
            }

            EndResponseStream();
        }

        private async ValueTask WriteCoreAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            if (!_headWritten)
            {
                _headBuffer.Write(buffer.Span);
                var bytes = _headBuffer.ToArray();
                var split = IndexOfHeaderEnd(bytes);
                if (split < 0)
                {
                    return;
                }

                var headText = Encoding.ASCII.GetString(bytes, 0, split);
                var bodyOffset = split + 4;
                var statusAndHeaders = ParseHttp1ResponseHead(headText);
                _dropBody = string.Equals(_method, "HEAD", StringComparison.OrdinalIgnoreCase)
                    || statusAndHeaders.StatusCode is 204 or 304;
                _decodeChunkedBody = statusAndHeaders.ChunkedTransfer && !_dropBody;
                var bodyBytes = bytes.AsMemory(bodyOffset);
                var endWithHeaders = _dropBody || (!_decodeChunkedBody && IsZeroContentLength(statusAndHeaders.Headers));
                await _connection.WriteHeadersAsync(
                    _stream,
                    statusAndHeaders.StatusCode,
                    statusAndHeaders.Headers,
                    completeWrites: endWithHeaders,
                    cancellationToken);
                _headWritten = true;
                _endStreamSent = endWithHeaders;
                _headBuffer.SetLength(0);
                if (!_dropBody && !_endStreamSent)
                {
                    StartResponseStream();
                }

                if (!_dropBody && bodyBytes.Length > 0 && !_endStreamSent)
                {
                    await WriteResponseBodyAsync(bodyBytes, cancellationToken);
                }

                return;
            }

            if (!_dropBody && !_endStreamSent && buffer.Length > 0)
            {
                await WriteResponseBodyAsync(buffer, cancellationToken);
            }
        }

        private async ValueTask WriteResponseBodyAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken)
        {
            if (_decodeChunkedBody)
            {
                await WriteDecodedChunkedBodyAsync(buffer, cancellationToken);
                return;
            }

            await _connection.WriteDataAsync(_stream, buffer, completeWrites: false, cancellationToken);
        }

        private async ValueTask WriteDecodedChunkedBodyAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken)
        {
            _chunkBuffer.Position = _chunkBuffer.Length;
            _chunkBuffer.Write(buffer.Span);

            var bytes = _chunkBuffer.ToArray();
            var offset = 0;
            while (offset < bytes.Length && !_endStreamSent)
            {
                if (_chunkState == ChunkParserState.ReadingSize)
                {
                    var lineEnd = IndexOfCrlf(bytes, offset);
                    if (lineEnd < 0)
                    {
                        break;
                    }

                    var line = Encoding.ASCII.GetString(bytes, offset, lineEnd - offset);
                    var separator = line.IndexOf(';', StringComparison.Ordinal);
                    if (separator >= 0)
                    {
                        line = line[..separator];
                    }

                    if (!long.TryParse(line.Trim(), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out _chunkBytesRemaining)
                        || _chunkBytesRemaining < 0)
                    {
                        throw new IOException("Invalid chunked response body.");
                    }

                    offset = lineEnd + 2;
                    _chunkState = _chunkBytesRemaining == 0
                        ? ChunkParserState.ReadingTrailers
                        : ChunkParserState.ReadingData;
                    continue;
                }

                if (_chunkState == ChunkParserState.ReadingData)
                {
                    var available = bytes.Length - offset;
                    if (available <= 0)
                    {
                        break;
                    }

                    var take = (int)Math.Min(available, _chunkBytesRemaining);
                    await _connection.WriteDataAsync(_stream, bytes.AsMemory(offset, take), completeWrites: false, cancellationToken);
                    offset += take;
                    _chunkBytesRemaining -= take;
                    if (_chunkBytesRemaining == 0)
                    {
                        _chunkState = ChunkParserState.ReadingDataCrlf;
                    }

                    continue;
                }

                if (_chunkState == ChunkParserState.ReadingDataCrlf)
                {
                    if (bytes.Length - offset < 2)
                    {
                        break;
                    }

                    if (bytes[offset] != (byte)'\r' || bytes[offset + 1] != (byte)'\n')
                    {
                        throw new IOException("Invalid chunked response body.");
                    }

                    offset += 2;
                    _chunkState = ChunkParserState.ReadingSize;
                    continue;
                }

                if (_chunkState == ChunkParserState.ReadingTrailers)
                {
                    var lineEnd = IndexOfCrlf(bytes, offset);
                    if (lineEnd < 0)
                    {
                        break;
                    }

                    if (lineEnd == offset)
                    {
                        await _connection.WriteDataAsync(_stream, ReadOnlyMemory<byte>.Empty, completeWrites: true, cancellationToken);
                        _endStreamSent = true;
                        EndResponseStream();
                        _chunkState = ChunkParserState.Complete;
                        offset = lineEnd + 2;
                        continue;
                    }

                    offset = lineEnd + 2;
                    continue;
                }

                offset = bytes.Length;
            }

            var remaining = bytes.AsMemory(offset).ToArray();
            _chunkBuffer.SetLength(0);
            _chunkBuffer.Write(remaining);
        }

        private void StartResponseStream()
        {
            if (_responseStreamActive)
            {
                return;
            }

            _responseStreamActive = true;
            _connection._metrics.Http3StreamedResponse();
            _connection._metrics.Http3ResponseStreamStarted();
        }

        private void EndResponseStream()
        {
            if (!_responseStreamActive)
            {
                return;
            }

            _responseStreamActive = false;
            _connection._metrics.Http3ResponseStreamEnded();
        }

        private static ResponseHead ParseHttp1ResponseHead(string text)
        {
            var lines = text.Split("\r\n", StringSplitOptions.None);
            var statusParts = lines[0].Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            var statusCode = statusParts.Length >= 2 && int.TryParse(statusParts[1], out var parsed) ? parsed : 502;
            List<Http1HeaderField> headers = [];
            var chunkedTransfer = false;
            for (var index = 1; index < lines.Length; index++)
            {
                var colon = lines[index].IndexOf(':');
                if (colon <= 0)
                {
                    continue;
                }

                var name = lines[index][..colon].Trim().ToLowerInvariant();
                var value = lines[index][(colon + 1)..].Trim();
                if (string.Equals(name, "transfer-encoding", StringComparison.OrdinalIgnoreCase)
                    && value.Contains("chunked", StringComparison.OrdinalIgnoreCase))
                {
                    chunkedTransfer = true;
                }

                if (!IsHopByHopHeader(name))
                {
                    headers.Add(new Http1HeaderField(name, value));
                }
            }

            return new ResponseHead(statusCode, headers, chunkedTransfer);
        }

        private static bool IsZeroContentLength(IReadOnlyList<Http1HeaderField> headers)
        {
            return headers.Any(static header =>
                string.Equals(header.Name, "content-length", StringComparison.OrdinalIgnoreCase)
                && string.Equals(header.Value.Trim(), "0", StringComparison.Ordinal));
        }

        private static int IndexOfHeaderEnd(ReadOnlySpan<byte> bytes)
        {
            for (var index = 3; index < bytes.Length; index++)
            {
                if (bytes[index - 3] == (byte)'\r'
                    && bytes[index - 2] == (byte)'\n'
                    && bytes[index - 1] == (byte)'\r'
                    && bytes[index] == (byte)'\n')
                {
                    return index - 3;
                }
            }

            return -1;
        }

        private static int IndexOfCrlf(ReadOnlySpan<byte> bytes, int start)
        {
            for (var index = start + 1; index < bytes.Length; index++)
            {
                if (bytes[index - 1] == (byte)'\r' && bytes[index] == (byte)'\n')
                {
                    return index - 1;
                }
            }

            return -1;
        }

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }

    private enum ChunkParserState
    {
        ReadingSize,
        ReadingData,
        ReadingDataCrlf,
        ReadingTrailers,
        Complete
    }

    private readonly record struct ResponseHead(int StatusCode, IReadOnlyList<Http1HeaderField> Headers, bool ChunkedTransfer);
}
