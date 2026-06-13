using MDRAVA.BLL.ControlPlane.Headers;
using MDRAVA.BLL.ControlPlane.Forwarding;
using MDRAVA.BLL.ControlPlane.Http1;
using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.ControlPlane.Routing;
using MDRAVA.BLL.ControlPlane.RuntimeGuards;
using MDRAVA.BLL.ControlPlane.Timeouts;
using MDRAVA.BLL.ControlPlane.UpstreamSelection;
using MDRAVA.BLL.ControlPlane.Resilience;
using MDRAVA.BLL.ControlPlane.HealthChecks;
using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Metrics;
using MDRAVA.BLL.ControlPlane.RequestDiagnostics;
using MDRAVA.BLL.Http;
using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.Caching;
using Microsoft.Extensions.Logging;
using MDRAVA.INF.Proxy.RuntimeGuards;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using MDRAVA.INF.Proxy;
using MDRAVA.INF.Proxy.Forwarding;
using MDRAVA.INF.Proxy.Health;
using MDRAVA.INF.Proxy.Http1;
using MDRAVA.INF.Proxy.Http3;
using MDRAVA.INF.Observability;

namespace MDRAVA.INF.Proxy.Http2;

public sealed class Http2ClientConnection
{
    private static readonly byte[] ClientPreface = "PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"u8.ToArray();
    private readonly Stream _stream;
    private readonly IPEndPoint? _remoteEndPoint;
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
    private readonly Http3AltSvcPolicy _altSvcPolicy;
    private readonly CircuitBreakerStore _circuitBreakerStore;
    private readonly AcmeHttp01ChallengeResponder _acmeChallengeResponder;
    private readonly ProxyMetrics _metrics;
    private readonly RequestIdGenerator _requestIdGenerator;
    private readonly AccessLogEmitter _accessLogEmitter;
    private readonly ClientRateLimiter _rateLimiter;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly ConcurrentDictionary<int, StreamState> _streams = new();

    public Http2ClientConnection(
        Stream stream,
        IPEndPoint? remoteEndPoint,
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
        Http3AltSvcPolicy altSvcPolicy,
        CircuitBreakerStore circuitBreakerStore,
        AcmeHttp01ChallengeResponder acmeChallengeResponder,
        ProxyMetrics metrics,
        RequestIdGenerator requestIdGenerator,
        AccessLogEmitter accessLogEmitter,
        ClientRateLimiter rateLimiter,
        TimeProvider timeProvider,
        ILogger logger)
    {
        _stream = stream;
        _remoteEndPoint = remoteEndPoint;
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
        _altSvcPolicy = altSvcPolicy;
        _circuitBreakerStore = circuitBreakerStore;
        _acmeChallengeResponder = acmeChallengeResponder;
        _metrics = metrics;
        _requestIdGenerator = requestIdGenerator;
        _accessLogEmitter = accessLogEmitter;
        _rateLimiter = rateLimiter;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async ValueTask RunAsync(CancellationToken cancellationToken)
    {
        _metrics.Http2ConnectionAccepted();
        if (!await ReadPrefaceAsync(cancellationToken))
        {
            _metrics.Http2ProtocolError("bad_preface");
            return;
        }

        await SendSettingsAsync(cancellationToken);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var frame = await ReadFrameAsync(cancellationToken);
                if (frame is null)
                {
                    return;
                }

                if (!await HandleFrameAsync(frame.Value, cancellationToken))
                {
                    return;
                }
            }
        }
        catch (IOException exception)
        {
            _metrics.ClientPrematureDisconnect();
            _logger.LogDebug(exception, "HTTP/2 client connection ended with I/O failure.");
        }
    }

    private async ValueTask<bool> HandleFrameAsync(Http2Frame frame, CancellationToken cancellationToken)
    {
        switch (frame.Type)
        {
            case Http2FrameType.Data:
                return await HandleDataAsync(frame, cancellationToken);
            case Http2FrameType.Headers:
                return await HandleHeadersAsync(frame, cancellationToken);
            case Http2FrameType.Priority:
                return true;
            case Http2FrameType.RstStream:
                _streams.TryRemove(frame.StreamId, out _);
                return true;
            case Http2FrameType.Settings:
                if ((frame.Flags & Http2Flags.Ack) == 0)
                {
                    await WriteFrameAsync(Http2FrameType.Settings, Http2Flags.Ack, 0, ReadOnlyMemory<byte>.Empty, cancellationToken);
                }

                return true;
            case Http2FrameType.Ping:
                if (frame.Payload.Length == 8 && (frame.Flags & Http2Flags.Ack) == 0)
                {
                    await WriteFrameAsync(Http2FrameType.Ping, Http2Flags.Ack, 0, frame.Payload, cancellationToken);
                }

                return true;
            case Http2FrameType.GoAway:
                return false;
            case Http2FrameType.WindowUpdate:
                return true;
            case Http2FrameType.Continuation:
                return await HandleContinuationAsync(frame, cancellationToken);
            default:
                return true;
        }
    }

    private async ValueTask<bool> HandleHeadersAsync(Http2Frame frame, CancellationToken cancellationToken)
    {
        if (frame.StreamId <= 0 || frame.StreamId % 2 == 0)
        {
            _metrics.Http2ProtocolError("invalid_stream_id");
            await SendGoAwayAsync(Http2ErrorCode.ProtocolError, cancellationToken);
            return false;
        }

        if (_streams.Count(static pair => !pair.Value.Completed) >= _listener.Http2Limits.MaxConcurrentStreams)
        {
            _metrics.Http2ProtocolError("max_concurrent_streams");
            await WriteRstStreamAsync(frame.StreamId, Http2ErrorCode.RefusedStream, cancellationToken);
            return true;
        }

        var stream = _streams.GetOrAdd(frame.StreamId, id => new StreamState(id));
        if (stream.HeadersComplete)
        {
            _metrics.Http2ProtocolError("duplicate_headers");
            await WriteRstStreamAsync(frame.StreamId, Http2ErrorCode.ProtocolError, cancellationToken);
            return true;
        }

        var payload = StripHeaderPaddingAndPriority(frame, out var valid);
        if (!valid)
        {
            _metrics.Http2ProtocolError("invalid_headers");
            await WriteRstStreamAsync(frame.StreamId, Http2ErrorCode.ProtocolError, cancellationToken);
            return true;
        }

        stream.HeaderBlock.Write(payload.Span);
        if (stream.HeaderBlock.Length > _listener.Http2Limits.MaxHeaderListBytes)
        {
            _metrics.Http2ProtocolError("header_list_too_large");
            await WriteRstStreamAsync(frame.StreamId, Http2ErrorCode.EnhanceYourCalm, cancellationToken);
            return true;
        }

        if ((frame.Flags & Http2Flags.EndHeaders) != 0)
        {
            stream.HeadersComplete = true;
        }

        if ((frame.Flags & Http2Flags.EndStream) != 0)
        {
            stream.EndStreamReceived = true;
        }

        await TryProcessCompleteStreamAsync(stream, cancellationToken);
        return true;
    }

    private async ValueTask<bool> HandleContinuationAsync(Http2Frame frame, CancellationToken cancellationToken)
    {
        if (!_streams.TryGetValue(frame.StreamId, out var stream) || stream.HeadersComplete)
        {
            _metrics.Http2ProtocolError("unexpected_continuation");
            await WriteRstStreamAsync(frame.StreamId, Http2ErrorCode.ProtocolError, cancellationToken);
            return true;
        }

        stream.HeaderBlock.Write(frame.Payload.Span);
        if (stream.HeaderBlock.Length > _listener.Http2Limits.MaxHeaderListBytes)
        {
            _metrics.Http2ProtocolError("header_list_too_large");
            await WriteRstStreamAsync(frame.StreamId, Http2ErrorCode.EnhanceYourCalm, cancellationToken);
            return true;
        }

        if ((frame.Flags & Http2Flags.EndHeaders) != 0)
        {
            stream.HeadersComplete = true;
        }

        await TryProcessCompleteStreamAsync(stream, cancellationToken);
        return true;
    }

    private async ValueTask<bool> HandleDataAsync(Http2Frame frame, CancellationToken cancellationToken)
    {
        if (!_streams.TryGetValue(frame.StreamId, out var stream) || !stream.HeadersComplete)
        {
            _metrics.Http2ProtocolError("unexpected_data");
            await WriteRstStreamAsync(frame.StreamId, Http2ErrorCode.ProtocolError, cancellationToken);
            return true;
        }

        var payload = StripDataPadding(frame, out var valid);
        if (!valid)
        {
            _metrics.Http2ProtocolError("invalid_data");
            await WriteRstStreamAsync(frame.StreamId, Http2ErrorCode.ProtocolError, cancellationToken);
            return true;
        }

        if (stream.Body.Length + payload.Length > _configurationSnapshot.Limits.MaxRequestBodyBytes)
        {
            _metrics.RequestBodySizeRejected();
            await WriteGeneratedResponseAsync(
                frame.StreamId,
                413,
                "Payload Too Large",
                CreateRequestContext(),
                ProxyFailureKind.RequestPayloadTooLarge,
                "GET",
                cancellationToken);
            _streams.TryRemove(frame.StreamId, out _);
            return true;
        }

        stream.Body.Write(payload.Span);
        if ((frame.Flags & Http2Flags.EndStream) != 0)
        {
            stream.EndStreamReceived = true;
        }

        await TryProcessCompleteStreamAsync(stream, cancellationToken);
        return true;
    }

    private async ValueTask TryProcessCompleteStreamAsync(StreamState stream, CancellationToken cancellationToken)
    {
        if (!stream.HeadersComplete || !stream.EndStreamReceived || stream.ProcessingStarted)
        {
            return;
        }

        stream.ProcessingStarted = true;
        _metrics.Http2StreamStarted();
        try
        {
            await ProcessStreamAsync(stream, cancellationToken);
        }
        finally
        {
            stream.Completed = true;
            _streams.TryRemove(stream.Id, out _);
            _metrics.Http2StreamEnded();
        }
    }

    private async ValueTask ProcessStreamAsync(StreamState stream, CancellationToken cancellationToken)
    {
        var context = CreateRequestContext();
        try
        {
            var requestBuildResult = BuildRequest(stream);
            if (requestBuildResult is not Http2RequestBuildResult.Accepted acceptedRequest)
            {
                var rejectionReason = ((Http2RequestBuildResult.Rejected)requestBuildResult).Reason;
                _metrics.Http2ProtocolError(rejectionReason);
                await WriteGeneratedResponseAsync(
                    stream.Id,
                    400,
                    "Bad Request",
                    context,
                    ProxyFailureKind.ClientMalformedRequest,
                    "GET",
                    cancellationToken);
                CompleteContext(ref context);
                return;
            }

            var requestHead = acceptedRequest.RequestHead;
            _metrics.RequestReceived();
            _metrics.Http2RequestReceived();
            context.SetRequest(
                requestHead.Method,
                requestHead.Host,
                requestHead.Target,
                ProxyExternalRequestIdPolicy.Extract(requestHead));

            var forwardedHeaders = _forwardedHeadersPolicy.Build(
                requestHead,
                new ForwardedHeadersListener(
                    _listener.Transport == RuntimeListenerTransport.Https ? "https" : "http",
                    _listener.Port),
                _configurationSnapshot.ForwardedHeaders,
                ProxyClientAddressPolicy.ToForwardedHeadersPeer(_remoteEndPoint));
            context.SetClientEndpoint(forwardedHeaders.ResolvedClientEndpoint);

            if (_rateLimiter.AcquireRequest(
                forwardedHeaders.ResolvedClientAddress,
                _configurationSnapshot.Limits.RequestsPerMinutePerIp)
                is ClientRateLimitDecision.RejectedResult)
            {
                await WriteGeneratedResponseAsync(
                    stream.Id,
                    429,
                    "Too Many Requests",
                    context,
                    ProxyFailureKind.RateLimited,
                    requestHead.Method,
                    cancellationToken);
                CompleteContext(ref context);
                return;
            }

            if (ProxyRequestMethodPolicy.IsConnectTunnelMethod(requestHead.Method))
            {
                _metrics.UnsupportedRequestFramingRejected();
                await WriteGeneratedResponseAsync(
                    stream.Id,
                    501,
                    "Not Implemented",
                    context,
                    ProxyFailureKind.ClientMalformedRequest,
                    requestHead.Method,
                    cancellationToken);
                CompleteContext(ref context);
                return;
            }

            if (_acmeChallengeResponder.CreateResponse(requestHead)
                is AcmeHttp01ChallengeResponseResult.HandledResult acmeChallengeResponse)
            {
                await WriteGeneratedRouteResponseAsync(stream.Id, acmeChallengeResponse.Response, context, requestHead.Method, cancellationToken);
                CompleteContext(ref context);
                return;
            }

            var routeMatch = _routeMatcher.Match(_configurationSnapshot.Routes, requestHead);
            if (routeMatch is null)
            {
                await WriteGeneratedResponseAsync(
                    stream.Id,
                    404,
                    "Not Found",
                    context,
                    ProxyFailureKind.NoMatchingRoute,
                    requestHead.Method,
                    cancellationToken);
                CompleteContext(ref context);
                return;
            }

            context.SetRoute(ProxyRequestContextRuntimeMapper.ToRequestRoute(routeMatch.Route));
            if (await TryHandleGeneratedRouteActionAsync(
                    stream.Id,
                    routeMatch.Route,
                    requestHead,
                    context,
                    cancellationToken))
            {
                CompleteContext(ref context);
                return;
            }

            if (await TryRejectKnownLengthRequestBodyAsync(
                    stream.Id,
                    routeMatch.Route,
                    requestHead,
                    context,
                    cancellationToken))
            {
                CompleteContext(ref context);
                return;
            }

            var upstreamTarget = _pathRewritePolicy.Apply(routeMatch.Route, requestHead.Target, requestHead.Path);
            var effectiveTimeouts = ProxyTimeoutPolicy.ApplyRouteTimeouts(routeMatch.Route, _configurationSnapshot.Timeouts);
            if (await TryHandleCacheHitAsync(
                    stream.Id,
                    routeMatch.Route,
                    requestHead,
                    upstreamTarget,
                    context,
                    cancellationToken))
            {
                CompleteContext(ref context);
                return;
            }

            var result = await ForwardWithRetriesAsync(
                stream.Id,
                stream.Body.ToArray(),
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
        }
        catch (Exception exception) when (exception is SocketException or IOException)
        {
            _metrics.ClientPrematureDisconnect();
            context.FailureKind = ProxyFailureKind.ClientDisconnected;
            CompleteContext(ref context);
        }
    }

    private async ValueTask<bool> TryHandleGeneratedRouteActionAsync(
        int streamId,
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

        await WriteGeneratedRouteResponseAsync(
            streamId,
            actionDecision.Response!,
            context,
            requestHead.Method,
            cancellationToken);
        return true;
    }

    private async ValueTask<bool> TryRejectKnownLengthRequestBodyAsync(
        int streamId,
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
        await WriteGeneratedResponseAsync(
            streamId,
            413,
            "Payload Too Large",
            context,
            ProxyFailureKind.RequestPayloadTooLarge,
            requestHead.Method,
            cancellationToken);
        return true;
    }

    private async ValueTask<bool> TryHandleCacheHitAsync(
        int streamId,
        RuntimeRoute route,
        Http1RequestHead requestHead,
        string upstreamTarget,
        ProxyRequestContext context,
        CancellationToken cancellationToken)
    {
        var cacheLookup = _cacheStore.Get(
            ProxyCacheRuntimeMapper.ToRequestScope(route, _listener),
            requestHead,
            upstreamTarget);
        if (cacheLookup is not ProxyCacheLookupResult.HitResult cacheHit)
        {
            return false;
        }

        await WriteCachedResponseAsync(streamId, requestHead, cacheHit.Response, context, cancellationToken);
        return true;
    }

    private Http2RequestBuildResult BuildRequest(StreamState stream)
    {
        if (!HpackCodec.TryDecodeRequestHeaders(stream.HeaderBlock.ToArray(), out var headers, out var rejectionReason))
        {
            return Http2RequestBuildResult.Reject(rejectionReason);
        }

        Dictionary<string, string> pseudo = new(StringComparer.Ordinal);
        List<ProxyHeaderField> regularHeaders = [];
        var regularHeaderSeen = false;
        foreach (var header in headers)
        {
            if (header.Name.Length == 0)
            {
                return Http2RequestBuildResult.Reject("empty_header_name");
            }

            if (header.Name.Any(static character => char.IsAsciiLetterUpper(character)))
            {
                return Http2RequestBuildResult.Reject("uppercase_header_name");
            }

            if (header.Name[0] == ':')
            {
                if (regularHeaderSeen
                    || pseudo.ContainsKey(header.Name)
                    || !Http2HeaderPolicy.IsAllowedRequestPseudoHeader(header.Name))
                {
                    return Http2RequestBuildResult.Reject("invalid_pseudo_header");
                }

                pseudo[header.Name] = header.Value;
                continue;
            }

            regularHeaderSeen = true;
            if (Http2HeaderPolicy.IsForbiddenRequestHeader(header.Name, header.Value))
            {
                return Http2RequestBuildResult.Reject("forbidden_header");
            }

            regularHeaders.Add(new ProxyHeaderField(header.Name, header.Value));
        }

        if (!pseudo.TryGetValue(":method", out var method)
            || !pseudo.TryGetValue(":scheme", out var scheme)
            || !pseudo.TryGetValue(":path", out var target))
        {
            return Http2RequestBuildResult.Reject("missing_pseudo_header");
        }

        if (!string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            return Http2RequestBuildResult.Reject("invalid_scheme");
        }

        if (pseudo.ContainsKey(":protocol") || ProxyRequestMethodPolicy.IsConnectTunnelMethod(method))
        {
            return Http2RequestBuildResult.Reject("extended_connect_unsupported");
        }

        if (!target.StartsWith('/'))
        {
            return Http2RequestBuildResult.Reject("invalid_path");
        }

        var authority = pseudo.TryGetValue(":authority", out var value)
            ? value
            : regularHeaders.FirstOrDefault(static header => string.Equals(header.Name, "host", StringComparison.OrdinalIgnoreCase))?.Value;
        if (string.IsNullOrWhiteSpace(authority))
        {
            return Http2RequestBuildResult.Reject("missing_authority");
        }

        var hostHeader = regularHeaders.FirstOrDefault(static header => string.Equals(header.Name, "host", StringComparison.OrdinalIgnoreCase));
        if (hostHeader is not null && !string.Equals(hostHeader.Value, authority, StringComparison.OrdinalIgnoreCase))
        {
            return Http2RequestBuildResult.Reject("authority_host_mismatch");
        }

        regularHeaders.RemoveAll(static header => string.Equals(header.Name, "host", StringComparison.OrdinalIgnoreCase));
        regularHeaders.Insert(0, new ProxyHeaderField("Host", authority));
        var bodyLength = stream.Body.Length;
        var framing = bodyLength > 0
            ? Http1RequestFraming.FromContentLength(bodyLength)
            : Http1RequestFraming.None;
        if (bodyLength > 0)
        {
            regularHeaders.Add(new ProxyHeaderField("Content-Length", bodyLength.ToString(CultureInfo.InvariantCulture)));
        }

        return Http2RequestBuildResult.Accept(
            new Http1RequestHead(
                method,
                target,
                ExtractPath(target),
                "HTTP/2",
                authority,
                framing,
                regularHeaders));
    }

    private abstract record Http2RequestBuildResult
    {
        private Http2RequestBuildResult()
        {
        }

        public static Http2RequestBuildResult Accept(Http1RequestHead requestHead)
        {
            return new Accepted(requestHead);
        }

        public static Http2RequestBuildResult Reject(string reason)
        {
            return new Rejected(reason);
        }

        public sealed record Accepted : Http2RequestBuildResult
        {
            public Accepted(Http1RequestHead requestHead)
            {
                ArgumentNullException.ThrowIfNull(requestHead);

                RequestHead = requestHead;
            }

            public Http1RequestHead RequestHead { get; }
        }

        public sealed record Rejected : Http2RequestBuildResult
        {
            public Rejected(string reason)
            {
                if (string.IsNullOrWhiteSpace(reason))
                {
                    throw new ArgumentException("HTTP/2 request rejection reason is required.", nameof(reason));
                }

                Reason = reason;
            }

            public string Reason { get; }
        }
    }

    private async ValueTask<ForwardingResult> ForwardWithRetriesAsync(
        int streamId,
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
        var retryPlan = ProxyRetryPolicy.CreatePlan(route, requestHead);
        if (retryPlan.Admission is ProxyRetryAdmissionDecision.SkippedDecision skippedAdmission)
        {
            _metrics.RetrySkipped(skippedAdmission.Reason);
        }

        var retryAllowed = retryPlan.IsAllowed;
        var maxAttempts = retryPlan.MaxAttempts;
        ForwardingResult? lastResult = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var selection = _upstreamSelector.Select(ProxyUpstreamSelectionRuntimeMapper.ToSelectionRoute(route));
            if (selection is null)
            {
                if (ProxyRetryPolicy.DidExhaustAttemptsBeforeUpstreamSelection(attempt))
                {
                    _metrics.RetryExhausted();
                }

                var failureResponse = ProxyGeneratedFailurePolicy.BuildFailureResponse(ProxyFailureKind.NoHealthyUpstream);
                await WriteGeneratedResponseAsync(
                    streamId,
                    failureResponse,
                    context,
                    requestHead.Method,
                    cancellationToken);
                return failureResponse.ToForwardingResult();
            }

            context.SetUpstream(ProxyRequestContextRuntimeMapper.ToRequestUpstream(selection.Upstream));
            var suppressGeneratedFailureResponse = ProxyRetryPolicy.ShouldSuppressAttemptFailureResponse(
                retryAllowed,
                attempt,
                maxAttempts);
            var translator = new Http2ResponseTranslationStream(
                this,
                streamId,
                requestHead.Method,
                _configurationSnapshot.Timeouts.DownstreamWriteTimeout,
                body);
            var result = await _forwarder.ForwardAsync(
                translator,
                Http1HeadReadResult.TranslatedRequestBody(body),
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
            ProxyUpstreamAttemptRecorder.Record(selection, result, _healthStore, _circuitBreakerStore);

            var retryAttempt = retryAllowed
                ? ProxyRetryPolicy.EvaluateAttempt(route.Retry, result, attempt, maxAttempts)
                : ProxyRetryAttemptDecision.Stop;
            if (retryAttempt == ProxyRetryAttemptDecision.Retry)
            {
                _metrics.RetryAttempted();
                if (route.Retry.RetryBackoff > TimeSpan.Zero)
                {
                    await Task.Delay(route.Retry.RetryBackoff, _timeProvider, cancellationToken);
                }

                continue;
            }

            if (retryAttempt is ProxyRetryAttemptDecision.SkippedDecision skippedAttempt)
            {
                _metrics.RetrySkipped(skippedAttempt.Reason);
            }

            if (retryAllowed && ProxyRetryPolicy.DidExhaustAttempts(route.Retry, result, attempt, maxAttempts))
            {
                _metrics.RetryExhausted();
            }

            if (suppressGeneratedFailureResponse
                && result is ForwardingResult.FailureResult { ResponseStarted: false } suppressedFailure)
            {
                return await WriteSuppressedFailureAsync(streamId, suppressedFailure, context, requestHead.Method, cancellationToken);
            }

            await translator.CompleteAsync(cancellationToken);
            return result;
        }

        if (lastResult is ForwardingResult.FailureResult { ResponseStarted: false } lastFailure)
        {
            return await WriteSuppressedFailureAsync(streamId, lastFailure, context, requestHead.Method, cancellationToken);
        }

        return lastResult ?? ForwardingResult.Failure(
            responseStarted: false,
            responseStatusCode: null,
            failureKind: ProxyFailureKind.NoHealthyUpstream);
    }

    private async ValueTask<ForwardingResult> WriteSuppressedFailureAsync(
        int streamId,
        ForwardingResult.FailureResult result,
        ProxyRequestContext context,
        string method,
        CancellationToken cancellationToken)
    {
        var response = ProxyGeneratedFailurePolicy.BuildFailureResponse(result);
        _metrics.GeneratedFailureResponse(response.StatusCode);

        await WriteGeneratedResponseAsync(
            streamId,
            response,
            context,
            method,
            cancellationToken);
        return response.ToForwardingResult();
    }

    private async ValueTask WriteCachedResponseAsync(
        int streamId,
        Http1RequestHead requestHead,
        CachedProxyResponse response,
        ProxyRequestContext context,
        CancellationToken cancellationToken)
    {
        var headers = ProxyCachedResponseHeaderPolicy.BuildFramedResponseHeaders(
            response,
            context.RequestId,
            _timeProvider.GetUtcNow()).ToList();
        AddAltSvcHeader(headers);
        var includeBody = !string.Equals(requestHead.Method, "HEAD", StringComparison.OrdinalIgnoreCase);
        await WriteHeadersAndBodyAsync(streamId, response.StatusCode, headers, includeBody ? response.Body : [], cancellationToken);
        context.ResponseStarted = true;
        context.ResponseStatusCode = response.StatusCode;
        context.KeepClientConnectionOpen = true;
        context.SetRouteAction("cache");
    }

    private async ValueTask WriteGeneratedRouteResponseAsync(
        int streamId,
        GeneratedRouteResponse response,
        ProxyRequestContext context,
        string method,
        CancellationToken cancellationToken)
    {
        var body = Encoding.UTF8.GetBytes(response.Body);
        var headers = GeneratedRouteResponseHeaderPolicy.BuildFramedResponseHeaders(
            response,
            context.RequestId,
            body.Length).ToList();
        AddAltSvcHeader(headers);
        await WriteHeadersAndBodyAsync(
            streamId,
            response.StatusCode,
            headers,
            string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase) ? [] : body,
            cancellationToken);
        context.ResponseStarted = true;
        context.ResponseStatusCode = response.StatusCode;
        context.KeepClientConnectionOpen = true;
    }

    private async ValueTask WriteGeneratedResponseAsync(
        int streamId,
        int statusCode,
        string body,
        ProxyRequestContext context,
        ProxyFailureKind failureKind,
        string method,
        CancellationToken cancellationToken)
    {
        await WriteGeneratedResponseAsync(
            streamId,
            ProxyGeneratedFailurePolicy.BuildFailureResponse(statusCode, body, failureKind),
            context,
            method,
            cancellationToken);
    }

    private async ValueTask WriteGeneratedResponseAsync(
        int streamId,
        ProxyGeneratedFailureResponse response,
        ProxyRequestContext context,
        string method,
        CancellationToken cancellationToken)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(response.Body);
        var headers = ProxyGeneratedFailurePolicy.BuildFramedResponseHeaders(
            response,
            context.RequestId,
            bodyBytes.Length).ToList();
        AddAltSvcHeader(headers);
        await WriteHeadersAndBodyAsync(
            streamId,
            response.StatusCode,
            headers,
            string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase) ? [] : bodyBytes,
            cancellationToken);
        context.ResponseStarted = true;
        context.ResponseStatusCode = response.StatusCode;
        context.FailureKind = response.FailureKind;
        context.KeepClientConnectionOpen = true;
    }

    private async ValueTask WriteHeadersAndBodyAsync(
        int streamId,
        int statusCode,
        IReadOnlyList<ProxyHeaderField> headers,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        var headerBlock = HpackCodec.EncodeResponseHeaders(statusCode, headers);
        await WriteFrameAsync(
            Http2FrameType.Headers,
            body.Length == 0 ? (byte)(Http2Flags.EndHeaders | Http2Flags.EndStream) : Http2Flags.EndHeaders,
            streamId,
            headerBlock,
            cancellationToken);
        if (body.Length > 0)
        {
            await WriteDataAsync(streamId, body, endStream: true, cancellationToken);
        }
    }

    private void AddAltSvcHeader(List<ProxyHeaderField> headers)
    {
        var projected = Http3AltSvcPolicy.ApplyHeader(headers, _altSvcPolicy.CreateHeader(_listener));
        headers.Clear();
        headers.AddRange(projected);
    }

    private async ValueTask WriteDataAsync(
        int streamId,
        ReadOnlyMemory<byte> body,
        bool endStream,
        CancellationToken cancellationToken)
    {
        var remaining = body;
        while (remaining.Length > 0)
        {
            var chunkLength = Math.Min(_listener.Http2Limits.MaxFrameSize, remaining.Length);
            var final = chunkLength == remaining.Length && endStream;
            await WriteFrameAsync(
                Http2FrameType.Data,
                final ? Http2Flags.EndStream : (byte)0,
                streamId,
                remaining[..chunkLength],
                cancellationToken);
            remaining = remaining[chunkLength..];
        }

        if (body.Length == 0 && endStream)
        {
            await WriteFrameAsync(Http2FrameType.Data, Http2Flags.EndStream, streamId, ReadOnlyMemory<byte>.Empty, cancellationToken);
        }
    }

    private async ValueTask SendSettingsAsync(CancellationToken cancellationToken)
    {
        var payload = new byte[18];
        WriteSetting(payload.AsSpan(0, 6), 3, (uint)_listener.Http2Limits.MaxConcurrentStreams);
        WriteSetting(payload.AsSpan(6, 6), 5, (uint)_listener.Http2Limits.MaxFrameSize);
        WriteSetting(payload.AsSpan(12, 6), 6, (uint)_listener.Http2Limits.MaxHeaderListBytes);
        await WriteFrameAsync(Http2FrameType.Settings, 0, 0, payload, cancellationToken);
    }

    private async ValueTask SendGoAwayAsync(Http2ErrorCode error, CancellationToken cancellationToken)
    {
        var payload = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4, 4), (uint)error);
        await WriteFrameAsync(Http2FrameType.GoAway, 0, 0, payload, cancellationToken);
    }

    private ValueTask WriteRstStreamAsync(int streamId, Http2ErrorCode error, CancellationToken cancellationToken)
    {
        var payload = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(payload, (uint)error);
        return WriteFrameAsync(Http2FrameType.RstStream, 0, streamId, payload, cancellationToken);
    }

    private async ValueTask WriteFrameAsync(
        Http2FrameType type,
        byte flags,
        int streamId,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        var header = new byte[9];
        header[0] = (byte)((payload.Length >> 16) & 0xff);
        header[1] = (byte)((payload.Length >> 8) & 0xff);
        header[2] = (byte)(payload.Length & 0xff);
        header[3] = (byte)type;
        header[4] = flags;
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(5, 4), (uint)streamId & 0x7fffffff);
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            await _stream.WriteAsync(header, cancellationToken);
            if (payload.Length > 0)
            {
                await _stream.WriteAsync(payload, cancellationToken);
            }
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async ValueTask<Http2Frame?> ReadFrameAsync(CancellationToken cancellationToken)
    {
        var header = await ReadExactAsync(9, cancellationToken);
        if (header.Length == 0)
        {
            return null;
        }

        var length = header[0] << 16 | header[1] << 8 | header[2];
        if (length > _listener.Http2Limits.MaxFrameSize)
        {
            _metrics.Http2ProtocolError("frame_too_large");
            await SendGoAwayAsync(Http2ErrorCode.FrameSizeError, cancellationToken);
            return null;
        }

        var type = (Http2FrameType)header[3];
        var flags = header[4];
        var streamId = (int)(BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(5, 4)) & 0x7fffffff);
        var payload = length == 0 ? [] : await ReadExactAsync(length, cancellationToken);
        return new Http2Frame(type, flags, streamId, payload);
    }

    private async ValueTask<byte[]> ReadExactAsync(int length, CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await _stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken);
            if (read == 0)
            {
                return offset == 0 ? [] : throw new IOException("HTTP/2 peer closed mid-frame.");
            }

            offset += read;
            _metrics.AddBytesRead(read);
        }

        return buffer;
    }

    private async ValueTask<bool> ReadPrefaceAsync(CancellationToken cancellationToken)
    {
        var preface = await ReadExactAsync(ClientPreface.Length, cancellationToken);
        return preface.AsSpan().SequenceEqual(ClientPreface);
    }

    private ProxyRequestContext CreateRequestContext()
    {
        return new ProxyRequestContext(
            _requestIdGenerator.Create(),
            _listener.Name,
            ProxyRequestContextRuntimeMapper.ToTransport(_listener),
            _remoteEndPoint?.ToString(),
            _configurationSnapshot.Version,
            _timeProvider,
            "http2");
    }

    private void CompleteContext(ref ProxyRequestContext context)
    {
        _accessLogEmitter.Complete(
            context,
            context.AccessLogEnabled ?? _configurationSnapshot.Observability.AccessLogEnabled,
            _configurationSnapshot.Observability.RecentDiagnosticsCapacity);
    }

    private static void ApplyForwardingResult(ProxyRequestContext context, ForwardingResult result)
    {
        context.ResponseStarted = result.ResponseStarted;
        context.ResponseStatusCode = result.ResponseStatusCode;
        context.KeepClientConnectionOpen = true;
        context.FailureKind = result.FailureKind;
    }

    private static string ExtractPath(string target)
    {
        var queryIndex = target.IndexOf('?');
        return queryIndex < 0 ? target : target[..queryIndex];
    }

    private static ReadOnlyMemory<byte> StripHeaderPaddingAndPriority(Http2Frame frame, out bool valid)
    {
        valid = true;
        var payload = frame.Payload;
        if ((frame.Flags & Http2Flags.Padded) != 0)
        {
            if (payload.Length == 0)
            {
                valid = false;
                return ReadOnlyMemory<byte>.Empty;
            }

            var padding = payload.Span[0];
            payload = payload[1..];
            if (padding > payload.Length)
            {
                valid = false;
                return ReadOnlyMemory<byte>.Empty;
            }

            payload = payload[..^padding];
        }

        if ((frame.Flags & Http2Flags.Priority) != 0)
        {
            if (payload.Length < 5)
            {
                valid = false;
                return ReadOnlyMemory<byte>.Empty;
            }

            payload = payload[5..];
        }

        return payload;
    }

    private static ReadOnlyMemory<byte> StripDataPadding(Http2Frame frame, out bool valid)
    {
        valid = true;
        var payload = frame.Payload;
        if ((frame.Flags & Http2Flags.Padded) == 0)
        {
            return payload;
        }

        if (payload.Length == 0)
        {
            valid = false;
            return ReadOnlyMemory<byte>.Empty;
        }

        var padding = payload.Span[0];
        payload = payload[1..];
        if (padding > payload.Length)
        {
            valid = false;
            return ReadOnlyMemory<byte>.Empty;
        }

        return payload[..^padding];
    }

    private static void WriteSetting(Span<byte> destination, ushort id, uint value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(destination[..2], id);
        BinaryPrimitives.WriteUInt32BigEndian(destination[2..], value);
    }

    private sealed class Http2ResponseTranslationStream : Stream
    {
        private readonly Http2ClientConnection _connection;
        private readonly int _streamId;
        private readonly string _method;
        private readonly TimeSpan _writeTimeout;
        private readonly MemoryStream _requestBody;
        private readonly MemoryStream _headBuffer = new();
        private bool _headWritten;
        private bool _endStreamSent;
        private bool _dropBody;

        public Http2ResponseTranslationStream(
            Http2ClientConnection connection,
            int streamId,
            string method,
            TimeSpan writeTimeout,
            byte[] requestBody)
        {
            _connection = connection;
            _streamId = streamId;
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
            await ProxyTimeoutPolicy.RunAsync(
                async timeoutToken => await WriteCoreAsync(buffer, timeoutToken),
                _writeTimeout,
                ProxyTimeoutKind.DownstreamWrite,
                cancellationToken);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        }

        public async ValueTask CompleteAsync(CancellationToken cancellationToken)
        {
            if (_headWritten && !_endStreamSent)
            {
                await _connection.WriteDataAsync(_streamId, ReadOnlyMemory<byte>.Empty, endStream: true, cancellationToken);
                _endStreamSent = true;
            }
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

                var bodyOffset = split + 4;
                if (!Http1ResponseParser.TryParse(bytes.AsSpan(0, bodyOffset), _method, out var responseHead, out var parseError))
                {
                    throw new IOException($"Invalid HTTP/1 response head: {parseError}.");
                }

                _dropBody = Http1ResponseParser.IsNoBodyResponse(_method, responseHead.StatusCode);
                var headers = new HopByHopHeaderPolicy().FilterForForwarding(
                    responseHead.Headers,
                    preserveTransferEncoding: false,
                    preserveTrailer: false);
                var bodyBytes = bytes.AsMemory(bodyOffset);
                var endWithHeaders = _dropBody || responseHead.Framing.Kind == Http1BodyKind.None;
                await _connection.WriteFrameAsync(
                    Http2FrameType.Headers,
                    endWithHeaders ? (byte)(Http2Flags.EndHeaders | Http2Flags.EndStream) : Http2Flags.EndHeaders,
                    _streamId,
                    HpackCodec.EncodeResponseHeaders(responseHead.StatusCode, headers),
                    cancellationToken);
                _headWritten = true;
                _endStreamSent = endWithHeaders;
                _headBuffer.SetLength(0);
                if (!_dropBody && bodyBytes.Length > 0 && !_endStreamSent)
                {
                    await _connection.WriteDataAsync(_streamId, bodyBytes, endStream: false, cancellationToken);
                }

                return;
            }

            if (!_dropBody && !_endStreamSent && buffer.Length > 0)
            {
                await _connection.WriteDataAsync(_streamId, buffer, endStream: false, cancellationToken);
            }
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

    internal static class HpackCodec
    {
        private static readonly HeaderField[] StaticTable =
        [
            new("", ""),
            new(":authority", ""),
            new(":method", "GET"),
            new(":method", "POST"),
            new(":path", "/"),
            new(":path", "/index.html"),
            new(":scheme", "http"),
            new(":scheme", "https"),
            new(":status", "200"),
            new(":status", "204"),
            new(":status", "206"),
            new(":status", "304"),
            new(":status", "400"),
            new(":status", "404"),
            new(":status", "500"),
            new("accept-charset", ""),
            new("accept-encoding", "gzip, deflate"),
            new("accept-language", ""),
            new("accept-ranges", ""),
            new("accept", ""),
            new("access-control-allow-origin", ""),
            new("age", ""),
            new("allow", ""),
            new("authorization", ""),
            new("cache-control", ""),
            new("content-disposition", ""),
            new("content-encoding", ""),
            new("content-language", ""),
            new("content-length", ""),
            new("content-location", ""),
            new("content-range", ""),
            new("content-type", ""),
            new("cookie", ""),
            new("date", ""),
            new("etag", ""),
            new("expect", ""),
            new("expires", ""),
            new("from", ""),
            new("host", ""),
            new("if-match", ""),
            new("if-modified-since", ""),
            new("if-none-match", ""),
            new("if-range", ""),
            new("if-unmodified-since", ""),
            new("last-modified", ""),
            new("link", ""),
            new("location", ""),
            new("max-forwards", ""),
            new("proxy-authenticate", ""),
            new("proxy-authorization", ""),
            new("range", ""),
            new("referer", ""),
            new("refresh", ""),
            new("retry-after", ""),
            new("server", ""),
            new("set-cookie", ""),
            new("strict-transport-security", ""),
            new("transfer-encoding", ""),
            new("user-agent", ""),
            new("vary", ""),
            new("via", ""),
            new("www-authenticate", "")
        ];
        private static readonly uint[] HuffmanCodes =
        [
            0x1ff8u, 0x7fffd8u, 0xfffffe2u, 0xfffffe3u, 0xfffffe4u, 0xfffffe5u, 0xfffffe6u, 0xfffffe7u,
            0xfffffe8u, 0xffffeau, 0x3ffffffcu, 0xfffffe9u, 0xfffffeau, 0x3ffffffdu, 0xfffffebu, 0xfffffecu,
            0xfffffedu, 0xfffffeeu, 0xfffffefu, 0xffffff0u, 0xffffff1u, 0xffffff2u, 0x3ffffffeu, 0xffffff3u,
            0xffffff4u, 0xffffff5u, 0xffffff6u, 0xffffff7u, 0xffffff8u, 0xffffff9u, 0xffffffau, 0xffffffbu,
            0x14u, 0x3f8u, 0x3f9u, 0xffau, 0x1ff9u, 0x15u, 0xf8u, 0x7fau,
            0x3fau, 0x3fbu, 0xf9u, 0x7fbu, 0xfau, 0x16u, 0x17u, 0x18u,
            0x0u, 0x1u, 0x2u, 0x19u, 0x1au, 0x1bu, 0x1cu, 0x1du,
            0x1eu, 0x1fu, 0x5cu, 0xfbu, 0x7ffcu, 0x20u, 0xffbu, 0x3fcu,
            0x1ffau, 0x21u, 0x5du, 0x5eu, 0x5fu, 0x60u, 0x61u, 0x62u,
            0x63u, 0x64u, 0x65u, 0x66u, 0x67u, 0x68u, 0x69u, 0x6au,
            0x6bu, 0x6cu, 0x6du, 0x6eu, 0x6fu, 0x70u, 0x71u, 0x72u,
            0xfcu, 0x73u, 0xfdu, 0x1ffbu, 0x7fff0u, 0x1ffcu, 0x3ffcu, 0x22u,
            0x7ffdu, 0x3u, 0x23u, 0x4u, 0x24u, 0x5u, 0x25u, 0x26u,
            0x27u, 0x6u, 0x74u, 0x75u, 0x28u, 0x29u, 0x2au, 0x7u,
            0x2bu, 0x76u, 0x2cu, 0x8u, 0x9u, 0x2du, 0x77u, 0x78u,
            0x79u, 0x7au, 0x7bu, 0x7ffeu, 0x7fcu, 0x3ffdu, 0x1ffdu, 0xffffffcu,
            0xfffe6u, 0x3fffd2u, 0xfffe7u, 0xfffe8u, 0x3fffd3u, 0x3fffd4u, 0x3fffd5u, 0x7fffd9u,
            0x3fffd6u, 0x7fffdau, 0x7fffdbu, 0x7fffdcu, 0x7fffddu, 0x7fffdeu, 0xffffebu, 0x7fffdfu,
            0xffffecu, 0xffffedu, 0x3fffd7u, 0x7fffe0u, 0xffffeeu, 0x7fffe1u, 0x7fffe2u, 0x7fffe3u,
            0x7fffe4u, 0x1fffdcu, 0x3fffd8u, 0x7fffe5u, 0x3fffd9u, 0x7fffe6u, 0x7fffe7u, 0xffffefu,
            0x3fffdau, 0x1fffddu, 0xfffe9u, 0x3fffdbu, 0x3fffdcu, 0x7fffe8u, 0x7fffe9u, 0x1fffdeu,
            0x7fffeau, 0x3fffddu, 0x3fffdeu, 0xfffff0u, 0x1fffdfu, 0x3fffdfu, 0x7fffebu, 0x7fffecu,
            0x1fffe0u, 0x1fffe1u, 0x3fffe0u, 0x1fffe2u, 0x7fffedu, 0x3fffe1u, 0x7fffeeu, 0x7fffefu,
            0xfffeau, 0x3fffe2u, 0x3fffe3u, 0x3fffe4u, 0x7ffff0u, 0x3fffe5u, 0x3fffe6u, 0x7ffff1u,
            0x3ffffe0u, 0x3ffffe1u, 0xfffebu, 0x7fff1u, 0x3fffe7u, 0x7ffff2u, 0x3fffe8u, 0x1ffffecu,
            0x3ffffe2u, 0x3ffffe3u, 0x3ffffe4u, 0x7ffffdeu, 0x7ffffdfu, 0x3ffffe5u, 0xfffff1u, 0x1ffffedu,
            0x7fff2u, 0x1fffe3u, 0x3ffffe6u, 0x7ffffe0u, 0x7ffffe1u, 0x3ffffe7u, 0x7ffffe2u, 0xfffff2u,
            0x1fffe4u, 0x1fffe5u, 0x3ffffe8u, 0x3ffffe9u, 0xffffffdu, 0x7ffffe3u, 0x7ffffe4u, 0x7ffffe5u,
            0xfffecu, 0xfffff3u, 0xfffedu, 0x1fffe6u, 0x3fffe9u, 0x1fffe7u, 0x1fffe8u, 0x7ffff3u,
            0x3fffeau, 0x3fffebu, 0x1ffffeeu, 0x1ffffefu, 0xfffff4u, 0xfffff5u, 0x3ffffeau, 0x7ffff4u,
            0x3ffffebu, 0x7ffffe6u, 0x3ffffecu, 0x3ffffedu, 0x7ffffe7u, 0x7ffffe8u, 0x7ffffe9u, 0x7ffffeau,
            0x7ffffebu, 0xffffffeu, 0x7ffffecu, 0x7ffffedu, 0x7ffffeeu, 0x7ffffefu, 0x7fffff0u, 0x3ffffeeu,
            0x3fffffffu
        ];

        private static readonly byte[] HuffmanCodeLengths =
        [
            13, 23, 28, 28, 28, 28, 28, 28, 28, 24, 30, 28, 28, 30, 28, 28,
            28, 28, 28, 28, 28, 28, 30, 28, 28, 28, 28, 28, 28, 28, 28, 28,
            6, 10, 10, 12, 13, 6, 8, 11, 10, 10, 8, 11, 8, 6, 6, 6,
            5, 5, 5, 6, 6, 6, 6, 6, 6, 6, 7, 8, 15, 6, 12, 10,
            13, 6, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
            7, 7, 7, 7, 7, 7, 7, 7, 8, 7, 8, 13, 19, 13, 14, 6,
            15, 5, 6, 5, 6, 5, 6, 6, 6, 5, 7, 7, 6, 6, 6, 5,
            6, 7, 6, 5, 5, 6, 7, 7, 7, 7, 7, 15, 11, 14, 13, 28,
            20, 22, 20, 20, 22, 22, 22, 23, 22, 23, 23, 23, 23, 23, 24, 23,
            24, 24, 22, 23, 24, 23, 23, 23, 23, 21, 22, 23, 22, 23, 23, 24,
            22, 21, 20, 22, 22, 23, 23, 21, 23, 22, 22, 24, 21, 22, 23, 23,
            21, 21, 22, 21, 23, 22, 23, 23, 20, 22, 22, 22, 23, 22, 22, 23,
            26, 26, 20, 19, 22, 23, 22, 25, 26, 26, 26, 27, 27, 26, 24, 25,
            19, 21, 26, 27, 27, 26, 27, 24, 21, 21, 26, 26, 28, 27, 27, 27,
            20, 24, 20, 21, 22, 21, 21, 23, 22, 22, 25, 25, 24, 24, 26, 23,
            26, 27, 26, 26, 27, 27, 27, 27, 27, 28, 27, 27, 27, 27, 27, 26,
            30
        ];

        private static readonly IReadOnlyDictionary<ulong, int> HuffmanDecodeTable = BuildHuffmanDecodeTable();

        public static bool TryDecodeRequestHeaders(
            byte[] block,
            out IReadOnlyList<ProxyHeaderField> headers,
            out string reason)
        {
            headers = [];
            reason = "invalid_hpack";
            List<ProxyHeaderField> decoded = [];
            List<HeaderField> dynamicTable = [];
            var offset = 0;
            while (offset < block.Length)
            {
                var current = block[offset];
                if ((current & 0x80) != 0)
                {
                    var index = DecodeInteger(block, 7, ref offset);
                    if (!TryGetHeader(index, dynamicTable, out var field))
                    {
                        reason = "invalid_hpack_index";
                        return false;
                    }

                    decoded.Add(new ProxyHeaderField(field.Name, field.Value));
                    continue;
                }

                if ((current & 0x40) != 0)
                {
                    if (!TryDecodeLiteral(block, 6, ref offset, dynamicTable, out var literal, out reason))
                    {
                        return false;
                    }

                    dynamicTable.Insert(0, literal);
                    decoded.Add(new ProxyHeaderField(literal.Name, literal.Value));
                    continue;
                }

                if ((current & 0x20) != 0)
                {
                    SkipInteger(block, 5, ref offset);
                    continue;
                }

                var prefix = (current & 0x10) != 0 ? 4 : 4;
                if (!TryDecodeLiteral(block, prefix, ref offset, dynamicTable, out var withoutIndex, out reason))
                {
                    return false;
                }

                decoded.Add(new ProxyHeaderField(withoutIndex.Name, withoutIndex.Value));
            }

            headers = decoded;
            return true;
        }

        public static byte[] EncodeRequestHeaders(IReadOnlyList<ProxyHeaderField> headers)
        {
            using var memory = new MemoryStream();
            foreach (var header in headers)
            {
                var name = header.Name.ToLowerInvariant();
                if (HopByHopHeaderPolicy.IsHopByHopHeader(name))
                {
                    continue;
                }

                WriteInteger(memory, 0x00, 4, StaticNameIndex(name));
                if (StaticNameIndex(name) == 0)
                {
                    WriteString(memory, name);
                }

                WriteString(memory, header.Value);
            }

            return memory.ToArray();
        }

        public static byte[] EncodeResponseHeaders(int statusCode, IReadOnlyList<ProxyHeaderField> headers)
        {
            using var memory = new MemoryStream();
            var indexedStatus = statusCode switch
            {
                200 => 8,
                204 => 9,
                206 => 10,
                304 => 11,
                400 => 12,
                404 => 13,
                500 => 14,
                _ => 0
            };
            if (indexedStatus > 0)
            {
                WriteInteger(memory, 0x80, 7, indexedStatus);
            }
            else
            {
                WriteInteger(memory, 0x00, 4, 8);
                WriteString(memory, statusCode.ToString(CultureInfo.InvariantCulture));
            }

            foreach (var header in headers)
            {
                if (HopByHopHeaderPolicy.IsHopByHopHeader(header.Name))
                {
                    continue;
                }

                WriteInteger(memory, 0x00, 4, StaticNameIndex(header.Name));
                if (StaticNameIndex(header.Name) == 0)
                {
                    WriteString(memory, header.Name.ToLowerInvariant());
                }

                WriteString(memory, header.Value);
            }

            return memory.ToArray();
        }

        private static bool TryDecodeLiteral(
            byte[] block,
            int prefixBits,
            ref int offset,
            IReadOnlyList<HeaderField> dynamicTable,
            out HeaderField field,
            out string reason)
        {
            field = new HeaderField("", "");
            reason = "invalid_hpack_literal";
            var nameIndex = DecodeInteger(block, prefixBits, ref offset);
            string name;
            if (nameIndex == 0)
            {
                if (!TryReadString(block, ref offset, out name, out reason))
                {
                    return false;
                }
            }
            else
            {
                if (!TryGetHeader(nameIndex, dynamicTable, out var indexed))
                {
                    reason = "invalid_hpack_index";
                    return false;
                }

                name = indexed.Name;
            }

            if (!TryReadString(block, ref offset, out var value, out reason))
            {
                return false;
            }

            field = new HeaderField(name, value);
            return true;
        }

        private static bool TryReadString(byte[] block, ref int offset, out string value, out string reason)
        {
            value = "";
            reason = "invalid_hpack_string";
            if (offset >= block.Length)
            {
                return false;
            }

            var huffman = (block[offset] & 0x80) != 0;
            var length = DecodeInteger(block, 7, ref offset);
            if (length < 0 || offset + length > block.Length)
            {
                return false;
            }

            if (huffman)
            {
                if (!TryDecodeHuffman(block.AsSpan(offset, length), out value))
                {
                    reason = "invalid_huffman";
                    return false;
                }
            }
            else
            {
                value = Encoding.ASCII.GetString(block, offset, length);
            }

            offset += length;
            return true;
        }

        private static bool TryDecodeHuffman(ReadOnlySpan<byte> bytes, out string value)
        {
            value = "";
            List<byte> decoded = [];
            var code = 0u;
            var length = 0;
            foreach (var current in bytes)
            {
                for (var bitIndex = 7; bitIndex >= 0; bitIndex--)
                {
                    code = (code << 1) | (uint)((current >> bitIndex) & 1);
                    length++;
                    if (HuffmanDecodeTable.TryGetValue(HuffmanKey(length, code), out var symbol))
                    {
                        if (symbol == 256)
                        {
                            return false;
                        }

                        decoded.Add((byte)symbol);
                        code = 0;
                        length = 0;
                        continue;
                    }

                    if (length > 30)
                    {
                        return false;
                    }
                }
            }

            if (length > 7)
            {
                return false;
            }

            if (length > 0 && code != ((1u << length) - 1))
            {
                return false;
            }

            value = Encoding.ASCII.GetString(decoded.ToArray());
            return true;
        }

        private static IReadOnlyDictionary<ulong, int> BuildHuffmanDecodeTable()
        {
            Dictionary<ulong, int> table = new();
            for (var symbol = 0; symbol < HuffmanCodes.Length; symbol++)
            {
                table[HuffmanKey(HuffmanCodeLengths[symbol], HuffmanCodes[symbol])] = symbol;
            }

            return table;
        }

        private static ulong HuffmanKey(int length, uint code)
        {
            return ((ulong)length << 32) | code;
        }

        private static int DecodeInteger(byte[] block, int prefixBits, ref int offset)
        {
            var mask = (1 << prefixBits) - 1;
            var value = block[offset++] & mask;
            if (value < mask)
            {
                return value;
            }

            var multiplier = 0;
            while (offset < block.Length)
            {
                var next = block[offset++];
                value += (next & 0x7f) << multiplier;
                if ((next & 0x80) == 0)
                {
                    break;
                }

                multiplier += 7;
            }

            return value;
        }

        private static void SkipInteger(byte[] block, int prefixBits, ref int offset)
        {
            DecodeInteger(block, prefixBits, ref offset);
        }

        private static void WriteInteger(Stream stream, byte prefix, int prefixBits, int value)
        {
            var maxPrefix = (1 << prefixBits) - 1;
            if (value < maxPrefix)
            {
                stream.WriteByte((byte)(prefix | value));
                return;
            }

            stream.WriteByte((byte)(prefix | maxPrefix));
            value -= maxPrefix;
            while (value >= 128)
            {
                stream.WriteByte((byte)(value % 128 + 128));
                value /= 128;
            }

            stream.WriteByte((byte)value);
        }

        private static void WriteString(Stream stream, string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            WriteInteger(stream, 0, 7, bytes.Length);
            stream.Write(bytes);
        }

        private static bool TryGetHeader(int index, IReadOnlyList<HeaderField> dynamicTable, out HeaderField field)
        {
            field = default;
            if (index > 0 && index < StaticTable.Length)
            {
                field = StaticTable[index];
                return true;
            }

            var dynamicIndex = index - StaticTable.Length;
            if (dynamicIndex >= 0 && dynamicIndex < dynamicTable.Count)
            {
                field = dynamicTable[dynamicIndex];
                return true;
            }

            return false;
        }

        private static int StaticNameIndex(string name)
        {
            for (var index = 1; index < StaticTable.Length; index++)
            {
                if (string.Equals(StaticTable[index].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return 0;
        }
    }

    private sealed class StreamState
    {
        public StreamState(int id)
        {
            Id = id;
        }

        public int Id { get; }

        public MemoryStream HeaderBlock { get; } = new();

        public MemoryStream Body { get; } = new();

        public bool HeadersComplete { get; set; }

        public bool EndStreamReceived { get; set; }

        public bool ProcessingStarted { get; set; }

        public bool Completed { get; set; }
    }

    private readonly record struct HeaderField(string Name, string Value);

    private readonly record struct Http2Frame(Http2FrameType Type, byte Flags, int StreamId, ReadOnlyMemory<byte> Payload);

    private enum Http2FrameType : byte
    {
        Data = 0x0,
        Headers = 0x1,
        Priority = 0x2,
        RstStream = 0x3,
        Settings = 0x4,
        PushPromise = 0x5,
        Ping = 0x6,
        GoAway = 0x7,
        WindowUpdate = 0x8,
        Continuation = 0x9
    }

    private enum Http2ErrorCode : uint
    {
        NoError = 0,
        ProtocolError = 1,
        InternalError = 2,
        FlowControlError = 3,
        SettingsTimeout = 4,
        StreamClosed = 5,
        FrameSizeError = 6,
        RefusedStream = 7,
        Cancel = 8,
        CompressionError = 9,
        ConnectError = 10,
        EnhanceYourCalm = 11
    }

    private static class Http2Flags
    {
        public const byte EndStream = 0x1;
        public const byte Ack = 0x1;
        public const byte EndHeaders = 0x4;
        public const byte Padded = 0x8;
        public const byte Priority = 0x20;
    }
}

