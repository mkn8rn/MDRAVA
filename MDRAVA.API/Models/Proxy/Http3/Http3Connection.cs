#pragma warning disable CA1416
using System.Net;
using System.Net.Quic;
using System.Text;
using MDRAVA.API.Proxy.Acme;
using MDRAVA.API.Proxy.Caching;
using MDRAVA.API.Proxy.Forwarding;
using MDRAVA.API.Proxy.Health;
using MDRAVA.API.Proxy.Metrics;
using MDRAVA.API.Proxy.Observability;
using MDRAVA.API.Proxy.Protocol;
using MDRAVA.API.Proxy.Runtime;

namespace MDRAVA.API.Proxy.Http3;

public sealed class Http3Connection
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
    private QuicStream? _localControlStream;

    public Http3Connection(
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
                    DrainUnsupportedStreamInBackground(stream, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (QuicException exception)
        {
            _logger.LogDebug(exception, "HTTP/3 QUIC connection ended.");
        }
        catch (IOException exception)
        {
            _logger.LogDebug(exception, "HTTP/3 connection ended with I/O failure.");
        }
        finally
        {
            if (_localControlStream is not null)
            {
                await _localControlStream.DisposeAsync();
            }

            _metrics.Http3ConnectionClosed();
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
            var headerRead = await ReadRequestHeadersAsync(stream, cancellationToken);
            var rejectionReason = headerRead.Reason;
            if (!headerRead.Success)
            {
                var closeConnection = RecordProtocolError(rejectionReason);
                await WriteGeneratedResponseAsync(stream, 400, "Bad Request", "Bad Request", context, "GET", cancellationToken);
                CompleteContext(ref context);
                return !closeConnection;
            }

            if (!Http3RequestTranslator.TryBuildRequest(
                    headerRead.Headers,
                    _listener,
                    out var requestHead,
                    out rejectionReason,
                    bodyMayFollow: true))
            {
                var closeConnection = RecordProtocolError(rejectionReason);
                await WriteGeneratedResponseAsync(stream, 400, "Bad Request", "Bad Request", context, "GET", cancellationToken);
                CompleteContext(ref context);
                return !closeConnection;
            }

            _metrics.RequestReceived();
            _metrics.Http3RequestReceived();
            context.SetRequest(requestHead.Method, requestHead.Host, requestHead.Target, ProxyExternalRequestIdPolicy.Extract(requestHead));

            if (!Http3RequestTranslator.IsSupportedMethod(requestHead.Method, out rejectionReason))
            {
                _metrics.Http3RequestRejected(rejectionReason);
                await WriteGeneratedResponseAsync(stream, 501, "Not Implemented", "Not Implemented", context, requestHead.Method, cancellationToken);
                CompleteContext(ref context);
                return true;
            }

            var noBodyFrames = requestHead.Framing.Kind == Http1BodyKind.None
                ? await EnsureNoRequestBodyFramesAsync(stream, cancellationToken)
                : Http3FrameValidationResult.Successful();
            if (!noBodyFrames.Success)
            {
                var closeConnection = RecordProtocolError(noBodyFrames.Reason);
                await WriteGeneratedResponseAsync(stream, 400, "Bad Request", "Bad Request", context, requestHead.Method, cancellationToken);
                CompleteContext(ref context);
                return !closeConnection;
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
            if (await TryHandleGeneratedRouteActionAsync(
                    stream,
                    routeMatch.Route,
                    requestHead,
                    context,
                    cancellationToken))
            {
                CompleteContext(ref context);
                return true;
            }

            if (await TryRejectKnownLengthRequestBodyAsync(
                    stream,
                    routeMatch.Route,
                    requestHead,
                    context,
                    cancellationToken))
            {
                CompleteContext(ref context);
                return true;
            }

            var requestBody = new Http3RequestBodyReadStream(
                this,
                stream,
                requestHead.Framing,
                cancellationToken);
            var upstreamTarget = _pathRewritePolicy.Apply(routeMatch.Route, requestHead.Target, requestHead.Path);
            var effectiveTimeouts = ProxyTimeoutPolicy.ApplyRouteTimeouts(routeMatch.Route, _configurationSnapshot.Timeouts);
            if (await TryHandleCacheHitAsync(
                    stream,
                    routeMatch.Route,
                    requestHead,
                    upstreamTarget,
                    context,
                    cancellationToken))
            {
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

            _logger.LogDebug(exception, "HTTP/3 stream ended with I/O failure.");
            CompleteContext(ref context);
            return true;
        }
        finally
        {
            _metrics.Http3StreamEnded();
        }
    }

    private async ValueTask<bool> TryHandleGeneratedRouteActionAsync(
        QuicStream stream,
        RuntimeRoute route,
        Http1RequestHead requestHead,
        ProxyRequestContext context,
        CancellationToken cancellationToken)
    {
        var actionDecision = _routeActionPolicy.Evaluate(
            route,
            requestHead,
            _listener,
            isUpgradeRequest: false);
        if (actionDecision.ShouldProxy)
        {
            return false;
        }

        _metrics.Http3GeneratedResponse();
        await WriteGeneratedRouteResponseAsync(
            stream,
            actionDecision.Response!,
            context,
            requestHead.Method,
            cancellationToken);
        return true;
    }

    private async ValueTask<bool> TryRejectKnownLengthRequestBodyAsync(
        QuicStream stream,
        RuntimeRoute route,
        Http1RequestHead requestHead,
        ProxyRequestContext context,
        CancellationToken cancellationToken)
    {
        if (requestHead.Framing.Kind != Http1BodyKind.ContentLength
            || requestHead.Framing.ContentLength.GetValueOrDefault() <= route.ResolvedOptions.MaxRequestBodyBytes)
        {
            return false;
        }

        _metrics.RequestBodySizeRejected();
        _metrics.Http3RequestRejected("request_body_too_large");
        await WriteGeneratedResponseAsync(
            stream,
            413,
            "Payload Too Large",
            "Payload Too Large",
            context,
            requestHead.Method,
            cancellationToken);
        return true;
    }

    private async ValueTask<bool> TryHandleCacheHitAsync(
        QuicStream stream,
        RuntimeRoute route,
        Http1RequestHead requestHead,
        string upstreamTarget,
        ProxyRequestContext context,
        CancellationToken cancellationToken)
    {
        if (!_cacheStore.TryGet(
                route,
                _listener,
                requestHead,
                upstreamTarget,
                out var cachedResponse)
            || cachedResponse is null)
        {
            return false;
        }

        await WriteCachedResponseAsync(stream, requestHead, cachedResponse, context, cancellationToken);
        return true;
    }

    private async ValueTask SendSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            _localControlStream = await _connection.OpenOutboundStreamAsync(QuicStreamType.Unidirectional, cancellationToken);
            using var payload = new MemoryStream();
            Http3Codec.WriteVarInt(payload, Http3Codec.ControlStream);
            using var settings = new MemoryStream();
            Http3Codec.WriteVarInt(settings, Http3Codec.QpackMaxTableCapacitySetting);
            Http3Codec.WriteVarInt(settings, 0);
            Http3Codec.WriteVarInt(settings, Http3Codec.QpackBlockedStreamsSetting);
            Http3Codec.WriteVarInt(settings, 0);
            Http3Codec.WriteFrame(payload, Http3Codec.SettingsFrame, settings.ToArray());
            // Chrome expects the server control stream to remain open after SETTINGS.
            await _localControlStream.WriteAsync(payload.ToArray(), completeWrites: false, cancellationToken);
        }
        catch (Exception exception) when (exception is QuicException or IOException)
        {
            _logger.LogDebug(exception, "HTTP/3 failed to send SETTINGS.");
        }
    }

    private void DrainUnsupportedStreamInBackground(
        QuicStream stream,
        CancellationToken cancellationToken)
    {
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await DrainUnsupportedStreamAsync(stream, cancellationToken);
                }
                catch (Exception exception) when (exception is OperationCanceledException or QuicException or IOException)
                {
                    _logger.LogDebug(exception, "HTTP/3 unidirectional stream ended.");
                }
            },
            CancellationToken.None);
    }

    private async ValueTask DrainUnsupportedStreamAsync(QuicStream stream, CancellationToken cancellationToken)
    {
        await using var ownedStream = stream;
        var buffer = new byte[256];
        while (await stream.ReadAsync(buffer, cancellationToken) > 0)
        {
        }
    }

    private async ValueTask<Http3HeaderReadResult> ReadRequestHeadersAsync(
        QuicStream stream,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var frame = await ReadFrameAsync(stream, cancellationToken);
            if (!frame.Success)
            {
                return Http3HeaderReadResult.Failure("invalid_frame");
            }

            if (frame.Payload.Length > MaxFramePayloadBytes)
            {
                return Http3HeaderReadResult.Failure("frame_too_large");
            }

            if (frame.Type == Http3Codec.DataFrame)
            {
                return Http3HeaderReadResult.Failure("unexpected_data");
            }

            if (frame.Type is Http3Codec.SettingsFrame or Http3Codec.GoAwayFrame)
            {
                return Http3HeaderReadResult.Failure("unexpected_control_frame");
            }

            if (frame.Type != Http3Codec.HeadersFrame)
            {
                return Http3HeaderReadResult.Failure("unsupported_frame");
            }

            if (!Http3Codec.TryDecodeHeaderBlock(
                    frame.Payload.Span,
                    _listener.Http2Limits.MaxHeaderListBytes,
                    out var headers,
                    out var rejectionReason))
            {
                return Http3HeaderReadResult.Failure(rejectionReason);
            }

            return Http3HeaderReadResult.Successful(headers);
        }
    }

    private static async ValueTask<Http3FrameValidationResult> EnsureNoRequestBodyFramesAsync(
        QuicStream stream,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var frame = await ReadFrameAsync(stream, cancellationToken);
            if (!frame.Success)
            {
                return Http3FrameValidationResult.Successful();
            }

            if (frame.Type == Http3Codec.DataFrame)
            {
                return Http3FrameValidationResult.Failure("unexpected_data");
            }

            var reason = frame.Type == Http3Codec.HeadersFrame
                ? "duplicate_headers"
                : "unexpected_control_frame";
            return Http3FrameValidationResult.Failure(reason);
        }
    }

    private static async ValueTask<Http3FrameReadResult> ReadFrameAsync(
        QuicStream stream,
        CancellationToken cancellationToken)
    {
        var type = await ReadVarIntAsync(stream, cancellationToken);
        var lengthResult = type.Success
            ? await ReadVarIntAsync(stream, cancellationToken)
            : Http3VarIntReadResult.Failure;
        if (!type.Success
            || !lengthResult.Success
            || lengthResult.Value < 0
            || lengthResult.Value > MaxFramePayloadBytes)
        {
            return Http3FrameReadResult.Failure;
        }

        var length = lengthResult.Value;
        var buffer = new byte[(int)length];
        if (!await TryReadExactAsync(stream, buffer, cancellationToken))
        {
            return Http3FrameReadResult.Failure;
        }

        return new Http3FrameReadResult(true, type.Value, buffer);
    }

    private static async ValueTask<Http3VarIntReadResult> ReadVarIntAsync(
        QuicStream stream,
        CancellationToken cancellationToken)
    {
        var firstBuffer = new byte[1];
        var firstRead = await stream.ReadAsync(firstBuffer, cancellationToken);
        if (firstRead == 0)
        {
            return Http3VarIntReadResult.Failure;
        }

        var first = firstBuffer[0];
        var length = 1 << (first >> 6);
        var value = first & 0x3f;
        if (length == 1)
        {
            return new Http3VarIntReadResult(true, value);
        }

        var rest = new byte[length - 1];
        if (!await TryReadExactAsync(stream, rest, cancellationToken))
        {
            return Http3VarIntReadResult.Failure;
        }

        foreach (var next in rest)
        {
            value = (value << 8) | next;
        }

        return new Http3VarIntReadResult(true, value);
    }

    private static async ValueTask<bool> TryReadExactAsync(
        QuicStream stream,
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < destination.Length)
        {
            var read = await stream.ReadAsync(destination[total..], cancellationToken);
            if (read == 0)
            {
                return false;
            }

            total += read;
        }

        return true;
    }

    private async ValueTask<ForwardingResult> ForwardWithRetriesAsync(
        QuicStream stream,
        Stream body,
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
        var retryAllowed = ProxyRetryPolicy.IsRetryAllowed(route, requestHead, out var skipReason);
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
                new Http1HeadReadResult(0, 0, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty),
                requestHead,
                route,
                selection.Upstream,
                _listener,
                ProxyTimeoutPolicy.ApplyRetryAttemptTimeout(route, timeouts),
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
                && ProxyRetryPolicy.ShouldRetry(route.Retry, result, attempt, maxAttempts, out finalSkipReason))
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

            if (retryAllowed && attempt == maxAttempts && ProxyRetryPolicy.IsRetryableFailure(route.Retry, result))
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
        var statusCode = result.ResponseStatusCode ?? ProxyForwardingFailurePolicy.StatusCodeForFailure(result.FailureKind);
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
            if (ProxyForwardingFailurePolicy.IsCircuitFailure(result.FailureKind))
            {
                _circuitBreakerStore.RecordFailure(
                    selection.CircuitBreakerLease,
                    ProxyForwardingFailurePolicy.CircuitFailureReason(result.FailureKind));
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
        List<Http1HeaderField> encodedHeaders = [new(":status", Http3Codec.StatusText(statusCode))];
        foreach (var header in headers)
        {
            if (!header.Name.StartsWith(':') && !IsHopByHopHeader(header.Name))
            {
                encodedHeaders.Add(new Http1HeaderField(header.Name.ToLowerInvariant(), header.Value));
            }
        }

        var headerBlock = Http3Codec.EncodeHeaderBlock(encodedHeaders);
        await WriteFrameAsync(stream, Http3Codec.HeadersFrame, headerBlock, completeWrites, cancellationToken);
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
                Http3Codec.DataFrame,
                remaining[..chunkLength],
                final,
                cancellationToken);
            _metrics.AddHttp3ResponseBytesSent(chunkLength);
            remaining = remaining[chunkLength..];
        }

        if (body.Length == 0 && completeWrites)
        {
            await WriteFrameAsync(stream, Http3Codec.DataFrame, ReadOnlyMemory<byte>.Empty, completeWrites: true, cancellationToken);
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
        Http3Codec.WriteFrame(frame, frameType, payload.Span);
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
            _configurationSnapshot.Version,
            "http3");
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
        if (context.ResponseStatusCode.HasValue)
        {
            _metrics.Http3RequestCompleted(context.Method, context.ResponseStatusCode, Http3Outcome(context));
        }

        context = null;
    }

    private bool RecordProtocolError(string reason)
    {
        _metrics.Http3ProtocolError(reason);
        return Interlocked.Increment(ref _protocolErrors) >= MaxProtocolErrorsPerConnection;
    }

    private static string Http3Outcome(ProxyRequestContext context)
    {
        if (context.FailureKind != ProxyFailureKind.None)
        {
            return "failure";
        }

        if (!context.ResponseStatusCode.HasValue)
        {
            return "aborted";
        }

        return context.ResponseStatusCode.Value < 400 ? "success" : "error";
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

    private sealed record Http3HeaderReadResult(
        bool Success,
        IReadOnlyList<Http1HeaderField> Headers,
        string Reason)
    {
        public static Http3HeaderReadResult Successful(IReadOnlyList<Http1HeaderField> headers)
        {
            return new Http3HeaderReadResult(true, headers, "");
        }

        public static Http3HeaderReadResult Failure(string reason)
        {
            return new Http3HeaderReadResult(false, [], reason);
        }
    }

    private readonly record struct Http3FrameValidationResult(bool Success, string Reason)
    {
        public static Http3FrameValidationResult Successful()
        {
            return new Http3FrameValidationResult(true, "");
        }

        public static Http3FrameValidationResult Failure(string reason)
        {
            return new Http3FrameValidationResult(false, reason);
        }
    }

    private readonly record struct Http3FrameReadResult(
        bool Success,
        long Type,
        ReadOnlyMemory<byte> Payload)
    {
        public static Http3FrameReadResult Failure { get; } = new(false, 0, ReadOnlyMemory<byte>.Empty);
    }

    private readonly record struct Http3VarIntReadResult(bool Success, long Value)
    {
        public static Http3VarIntReadResult Failure { get; } = new(false, 0);
    }

    private sealed class Http3RequestBodyReadStream : Stream
    {
        private readonly Http3Connection _connection;
        private readonly QuicStream _stream;
        private readonly Http1RequestFraming _framing;
        private readonly CancellationToken _connectionCancellationToken;
        private byte[] _pending = [];
        private int _pendingOffset;
        private long _remainingContentLength;
        private bool _completed;

        public Http3RequestBodyReadStream(
            Http3Connection connection,
            QuicStream stream,
            Http1RequestFraming framing,
            CancellationToken connectionCancellationToken)
        {
            _connection = connection;
            _stream = stream;
            _framing = framing;
            _connectionCancellationToken = connectionCancellationToken;
            _remainingContentLength = framing.ContentLength.GetValueOrDefault();
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer.AsMemory(offset, count), _connectionCancellationToken)
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (buffer.Length == 0)
            {
                return 0;
            }

            if (_framing.Kind == Http1BodyKind.None)
            {
                return 0;
            }

            while (!HasPending())
            {
                if (!await FillPendingAsync(cancellationToken).ConfigureAwait(false))
                {
                    return 0;
                }
            }

            var count = Math.Min(buffer.Length, _pending.Length - _pendingOffset);
            _pending.AsMemory(_pendingOffset, count).CopyTo(buffer);
            _pendingOffset += count;
            if (_pendingOffset >= _pending.Length)
            {
                _pending = [];
                _pendingOffset = 0;
            }

            return count;
        }

        private async ValueTask<bool> FillPendingAsync(CancellationToken cancellationToken)
        {
            if (_completed)
            {
                return false;
            }

            if (_framing.Kind == Http1BodyKind.ContentLength && _remainingContentLength <= 0)
            {
                _completed = true;
                return false;
            }

            while (true)
            {
                var frame = await ReadFrameAsync(_stream, cancellationToken).ConfigureAwait(false);
                if (!frame.Success)
                {
                    if (_framing.Kind == Http1BodyKind.ContentLength && _remainingContentLength > 0)
                    {
                        throw new IOException("HTTP/3 stream ended before the declared request body was complete.");
                    }

                    _completed = true;
                    if (_framing.Kind == Http1BodyKind.Chunked)
                    {
                        _pending = "0\r\n\r\n"u8.ToArray();
                        _pendingOffset = 0;
                        return true;
                    }

                    return false;
                }

                if (frame.Type != Http3Codec.DataFrame)
                {
                    _connection._metrics.Http3ProtocolError(frame.Type == Http3Codec.HeadersFrame
                        ? "duplicate_headers"
                        : "unexpected_control_frame");
                    throw new IOException("HTTP/3 request body stream contained a non-DATA frame.");
                }

                if (frame.Payload.Length == 0)
                {
                    continue;
                }

                _connection._metrics.AddHttp3RequestBodyBytesReceived(frame.Payload.Length);
                if (_framing.Kind == Http1BodyKind.ContentLength)
                {
                    if (frame.Payload.Length > _remainingContentLength)
                    {
                        _connection._metrics.Http3ProtocolError("invalid_content_length");
                        throw new IOException("HTTP/3 request body exceeded the declared Content-Length.");
                    }

                    _remainingContentLength -= frame.Payload.Length;
                    _pending = frame.Payload.ToArray();
                    _pendingOffset = 0;
                    return true;
                }

                _pending = BuildChunk(frame.Payload.Span);
                _pendingOffset = 0;
                return true;
            }
        }

        private bool HasPending()
        {
            return _pendingOffset < _pending.Length;
        }

        private static byte[] BuildChunk(ReadOnlySpan<byte> payload)
        {
            var prefix = Encoding.ASCII.GetBytes(payload.Length.ToString("x", System.Globalization.CultureInfo.InvariantCulture) + "\r\n");
            var chunk = new byte[prefix.Length + payload.Length + 2];
            prefix.CopyTo(chunk);
            payload.CopyTo(chunk.AsSpan(prefix.Length));
            chunk[^2] = (byte)'\r';
            chunk[^1] = (byte)'\n';
            return chunk;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class Http3ResponseTranslationStream : Stream
    {
        private readonly Http3Connection _connection;
        private readonly QuicStream _stream;
        private readonly string _method;
        private readonly TimeSpan _writeTimeout;
        private readonly Stream _requestBody;
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
            Http3Connection connection,
            QuicStream stream,
            string method,
            TimeSpan writeTimeout,
            Stream requestBody)
        {
            _connection = connection;
            _stream = stream;
            _method = method;
            _writeTimeout = writeTimeout;
            _requestBody = requestBody;
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
