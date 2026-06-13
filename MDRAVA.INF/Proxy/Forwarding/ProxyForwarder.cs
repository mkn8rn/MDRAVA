using MDRAVA.BLL.Http;
using MDRAVA.BLL.ControlPlane.Headers;
using MDRAVA.BLL.ControlPlane.Forwarding;
using MDRAVA.BLL.ControlPlane.Http1;
using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.ControlPlane.Resilience;
using MDRAVA.BLL.ControlPlane.Timeouts;
using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Metrics;
using MDRAVA.BLL.ControlPlane.Caching;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using MDRAVA.INF.Proxy.Connections;
using MDRAVA.INF.Proxy.Http1;
using MDRAVA.INF.Proxy.Http3;
using MDRAVA.INF.Proxy.Http2;
using MDRAVA.INF.Proxy;
using MDRAVA.INF.Observability;

namespace MDRAVA.INF.Proxy.Forwarding;

public sealed class ProxyForwarder
{
    private readonly UpstreamConnectionPool _upstreamConnections;
    private readonly Http3UpstreamConnectionPool _http3UpstreamConnections;
    private readonly ProxyMetrics _metrics;
    private readonly HopByHopHeaderPolicy _headerPolicy;
    private readonly ResponseCacheStore _cacheStore;
    private readonly Http3AltSvcPolicy _altSvcPolicy;
    private readonly ILogger<ProxyForwarder> _logger;

    public ProxyForwarder(
        UpstreamConnectionPool upstreamConnections,
        Http3UpstreamConnectionPool http3UpstreamConnections,
        ProxyMetrics metrics,
        HopByHopHeaderPolicy headerPolicy,
        ResponseCacheStore cacheStore,
        Http3AltSvcPolicy altSvcPolicy,
        ILogger<ProxyForwarder> logger)
    {
        _upstreamConnections = upstreamConnections;
        _http3UpstreamConnections = http3UpstreamConnections;
        _metrics = metrics;
        _headerPolicy = headerPolicy;
        _cacheStore = cacheStore;
        _altSvcPolicy = altSvcPolicy;
        _logger = logger;
    }

    public async ValueTask<ForwardingResult> ForwardAsync(
        Stream clientStream,
        Http1HeadReadResult requestHeadRead,
        Http1RequestHead requestHead,
        RuntimeRoute route,
        RuntimeUpstream upstream,
        RuntimeListener listener,
        RuntimeTimeouts timeouts,
        RuntimeConnectionLimits connectionLimits,
        RuntimeLimits limits,
        string upstreamTarget,
        ForwardedHeadersContext forwardedHeaders,
        bool preferClientKeepAlive,
        string requestId,
        CancellationToken cancellationToken,
        bool suppressGeneratedFailureResponse = false)
    {
        var responseStarted = false;
        UpstreamConnectionLease? upstreamLease = null;

        try
        {
            Http1BodyReader? preReadRequestBodyReader = null;
            byte[]? preReadChunkLine = null;

            if (requestHead.Framing.Kind == Http1BodyKind.Chunked)
            {
                preReadRequestBodyReader = new Http1BodyReader(clientStream, requestHeadRead.InitialBodyBytes, _metrics, timeouts.ClientRequestBodyIdleTimeout, ProxyTimeoutKind.ClientRequestBodyIdle);
                preReadChunkLine = await preReadRequestBodyReader.ReadLineWithCrlfAsync(listener.MaxChunkLineBytes, cancellationToken);
                if (!Http1ChunkSizeParser.TryParseLine(preReadChunkLine.AsSpan(), out _))
                {
                    throw new Http1ClientProtocolException("Invalid chunk-size line.");
                }
            }

            ResponseForwardingResult responseResult;
            if (RuntimeUpstreamProtocol.IsHttp3(upstream.Protocol))
            {
                responseResult = await ForwardHttp3Async(
                    clientStream,
                    requestHeadRead,
                    requestHead,
                    route,
                    upstream,
                    listener,
                    timeouts,
                    connectionLimits,
                    upstreamTarget,
                    forwardedHeaders,
                    preferClientKeepAlive,
                    requestId,
                    suppressGeneratedFailureResponse,
                    preReadRequestBodyReader,
                    preReadChunkLine,
                    () => responseStarted = true,
                    cancellationToken);
            }
            else
            {
                upstreamLease = await _upstreamConnections.BorrowAsync(
                    upstream,
                    timeouts,
                    connectionLimits,
                    cancellationToken);
                var upstreamStream = upstreamLease.Stream;

                responseResult = RuntimeUpstreamProtocol.IsHttp2(upstream.Protocol)
                    ? await ForwardHttp2Async(
                        upstreamStream,
                        clientStream,
                        requestHeadRead,
                        requestHead,
                        route,
                        upstream,
                        listener,
                        timeouts,
                        upstreamTarget,
                        forwardedHeaders,
                        preferClientKeepAlive,
                        requestId,
                        suppressGeneratedFailureResponse,
                        preReadRequestBodyReader,
                        preReadChunkLine,
                        () => responseStarted = true,
                        cancellationToken)
                    : await ForwardHttp1Async(
                        upstreamStream,
                        clientStream,
                        requestHeadRead,
                        requestHead,
                        route,
                        listener,
                        timeouts,
                        upstreamTarget,
                        forwardedHeaders,
                        preferClientKeepAlive,
                        requestId,
                        suppressGeneratedFailureResponse,
                        preReadRequestBodyReader,
                        preReadChunkLine,
                        () => responseStarted = true,
                        cancellationToken);
            }

            responseStarted = responseResult.ResponseStarted;

            if (responseResult.SuppressedForRetry)
            {
                _metrics.UpstreamFailed();
                return ForwardingResult.Failure(
                    responseStarted: false,
                    responseStatusCode: responseResult.StatusCode,
                    failureKind: ProxyFailureKind.UpstreamUnavailable);
            }

            if (responseResult.CanReuseUpstreamConnection && upstreamLease is not null)
            {
                upstreamLease.MarkReusable();
            }

            _metrics.UpstreamSucceeded();
            _logger.LogDebug(
                "Proxied {Method} {Target} to upstream {UpstreamName}",
                requestHead.Method,
                requestHead.Target,
                upstream.Name);
            return ForwardingResult.Success(
                responseStarted,
                responseResult.KeepClientConnectionOpen,
                responseResult.StatusCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ProxyTimeoutException exception)
        {
            var timeoutFailure = ProxyTimeoutFailurePolicy.ClassifyForwardingTimeout(exception.Kind, responseStarted);
            await HandleTimeoutAsync(clientStream, requestHead, upstream, responseStarted, exception, timeouts, requestId, cancellationToken, suppressGeneratedFailureResponse);
            return ForwardingResult.Failure(
                responseStarted,
                timeoutFailure.ResponseStatusCode,
                timeoutFailure.FailureKind);
        }
        catch (Http1PayloadTooLargeException exception)
        {
            _metrics.RequestBodySizeRejected();
            _metrics.ClientBodyRelayFailed();
            _logger.LogDebug(
                exception,
                "Rejected oversized request body for {Method} {Target}",
                requestHead.Method,
                requestHead.Target);

            if (ProxyGeneratedFailurePolicy.CanWriteFailureResponse(responseStarted, suppressGeneratedFailureResponse))
            {
                await ProxyGeneratedFailureWriter.WriteAsync(
                    clientStream,
                    ProxyFailureKind.RequestPayloadTooLarge,
                    timeouts,
                    requestId,
                    _metrics,
                    cancellationToken);
            }

            return ForwardingResult.Failure(
                responseStarted,
                ProxyForwardingFailurePolicy.ResponseStatusCodeForFailure(
                    responseStarted,
                    ProxyFailureKind.RequestPayloadTooLarge),
                ProxyFailureKind.RequestPayloadTooLarge);
        }
        catch (Http1ClientProtocolException exception)
        {
            _metrics.MalformedRequestRejected();
            _logger.LogDebug(
                exception,
                "Rejected malformed request body for {Method} {Target}",
                requestHead.Method,
                requestHead.Target);

            if (ProxyGeneratedFailurePolicy.CanWriteFailureResponse(responseStarted, suppressGeneratedFailureResponse))
            {
                await ProxyGeneratedFailureWriter.WriteAsync(
                    clientStream,
                    ProxyFailureKind.ClientMalformedRequest,
                    timeouts,
                    requestId,
                    _metrics,
                    cancellationToken);
            }
            return ForwardingResult.Failure(
                responseStarted,
                ProxyForwardingFailurePolicy.ResponseStatusCodeForFailure(
                    responseStarted,
                    ProxyFailureKind.ClientMalformedRequest),
                ProxyFailureKind.ClientMalformedRequest);
        }
        catch (Http1UpstreamProtocolException exception)
        {
            _metrics.UpstreamMalformedResponse();
            _metrics.UpstreamFailed();
            if (ProxyGeneratedFailurePolicy.CanWriteFailureResponse(responseStarted, suppressGeneratedFailureResponse))
            {
                _metrics.UpstreamConnectFailed();
            }
            _logger.LogWarning(
                exception,
                "Upstream response framing failed for {Method} {Target} to upstream {UpstreamName}",
                requestHead.Method,
                requestHead.Target,
                upstream.Name);

            if (ProxyGeneratedFailurePolicy.CanWriteFailureResponse(responseStarted, suppressGeneratedFailureResponse))
            {
                ProxyGeneratedFailureMetrics.Record(_metrics, ProxyFailureKind.UpstreamMalformedResponse);
                await ProxyGeneratedFailureWriter.WriteAsync(
                    clientStream,
                    ProxyFailureKind.UpstreamMalformedResponse,
                    timeouts,
                    requestId,
                    _metrics,
                    cancellationToken);
            }
            return ForwardingResult.Failure(
                responseStarted,
                ProxyForwardingFailurePolicy.ResponseStatusCodeForFailure(
                    responseStarted,
                    ProxyFailureKind.UpstreamMalformedResponse),
                ProxyFailureKind.UpstreamMalformedResponse);
        }
        catch (Http2UpstreamProtocolException exception)
        {
            _metrics.UpstreamHttp2ProtocolError();
            _metrics.UpstreamMalformedResponse();
            _metrics.UpstreamFailed();
            if (ProxyGeneratedFailurePolicy.CanWriteFailureResponse(responseStarted, suppressGeneratedFailureResponse))
            {
                _metrics.UpstreamConnectFailed();
            }
            _logger.LogWarning(
                exception,
                "Upstream HTTP/2 response framing failed for {Method} {Target} to upstream {UpstreamName}",
                requestHead.Method,
                requestHead.Target,
                upstream.Name);

            if (ProxyGeneratedFailurePolicy.CanWriteFailureResponse(responseStarted, suppressGeneratedFailureResponse))
            {
                ProxyGeneratedFailureMetrics.Record(_metrics, ProxyFailureKind.UpstreamMalformedResponse);
                await ProxyGeneratedFailureWriter.WriteAsync(
                    clientStream,
                    ProxyFailureKind.UpstreamMalformedResponse,
                    timeouts,
                    requestId,
                    _metrics,
                    cancellationToken);
            }
            return ForwardingResult.Failure(
                responseStarted,
                ProxyForwardingFailurePolicy.ResponseStatusCodeForFailure(
                    responseStarted,
                    ProxyFailureKind.UpstreamMalformedResponse),
                ProxyFailureKind.UpstreamMalformedResponse);
        }
        catch (Http3UpstreamProtocolException exception)
        {
            _metrics.UpstreamHttp3ProtocolError(
                exception.FailureKind == Http3UpstreamFailureKind.ConnectFailure ? "connect_failure" : "protocol_failure");
            _metrics.UpstreamFailed();
            var failureKind = exception.FailureKind == Http3UpstreamFailureKind.ConnectFailure && !responseStarted
                ? ProxyFailureKind.UpstreamConnectFailed
                : responseStarted
                    ? ProxyFailureKind.UpstreamPrematureDisconnect
                    : ProxyFailureKind.UpstreamMalformedResponse;
            if (failureKind == ProxyFailureKind.UpstreamMalformedResponse)
            {
                _metrics.UpstreamMalformedResponse();
            }

            if (ProxyGeneratedFailurePolicy.CanWriteFailureResponse(responseStarted, suppressGeneratedFailureResponse))
            {
                _metrics.UpstreamConnectFailed();
            }

            _logger.LogWarning(
                exception,
                "Upstream HTTP/3 forwarding failed for {Method} {Target} to upstream {UpstreamName}",
                requestHead.Method,
                requestHead.Target,
                upstream.Name);

            if (ProxyGeneratedFailurePolicy.CanWriteFailureResponse(responseStarted, suppressGeneratedFailureResponse))
            {
                ProxyGeneratedFailureMetrics.Record(_metrics, failureKind);
                await ProxyGeneratedFailureWriter.WriteAsync(
                    clientStream,
                    failureKind,
                    timeouts,
                    requestId,
                    _metrics,
                    cancellationToken);
            }

            return ForwardingResult.Failure(
                responseStarted,
                ProxyForwardingFailurePolicy.ResponseStatusCodeForFailure(responseStarted, failureKind),
                failureKind);
        }
        catch (UpstreamTlsException exception)
        {
            _metrics.UpstreamFailed();
            if (RuntimeUpstreamProtocol.IsHttp2(upstream.Protocol)
                && exception.Message.Contains("ALPN", StringComparison.OrdinalIgnoreCase))
            {
                _metrics.UpstreamHttp2AlpnFailed();
            }

            _logger.LogWarning(
                exception,
                "Upstream TLS failed for {Method} {Target} to upstream {UpstreamName}",
                requestHead.Method,
                requestHead.Target,
                upstream.Name);

            if (ProxyGeneratedFailurePolicy.CanWriteFailureResponse(responseStarted, suppressGeneratedFailureResponse))
            {
                ProxyGeneratedFailureMetrics.Record(_metrics, ProxyFailureKind.UpstreamConnectFailed);
                await ProxyGeneratedFailureWriter.WriteAsync(
                    clientStream,
                    ProxyFailureKind.UpstreamConnectFailed,
                    timeouts,
                    requestId,
                    _metrics,
                    cancellationToken);
            }

            var failureKind = responseStarted
                ? ProxyFailureKind.UpstreamPrematureDisconnect
                : ProxyFailureKind.UpstreamConnectFailed;

            return ForwardingResult.Failure(
                responseStarted,
                ProxyForwardingFailurePolicy.ResponseStatusCodeForFailure(responseStarted, failureKind),
                failureKind);
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

            if (ProxyGeneratedFailurePolicy.CanWriteFailureResponse(responseStarted, suppressGeneratedFailureResponse))
            {
                ProxyGeneratedFailureMetrics.Record(_metrics, ProxyFailureKind.UpstreamConnectFailed);
                await ProxyGeneratedFailureWriter.WriteAsync(
                    clientStream,
                    ProxyFailureKind.UpstreamConnectFailed,
                    timeouts,
                    requestId,
                    _metrics,
                    cancellationToken);
            }

            var failureKind = responseStarted
                ? ProxyFailureKind.UpstreamPrematureDisconnect
                : ProxyFailureKind.UpstreamConnectFailed;

            return ForwardingResult.Failure(
                responseStarted,
                ProxyForwardingFailurePolicy.ResponseStatusCodeForFailure(responseStarted, failureKind),
                failureKind);
        }
        finally
        {
            if (upstreamLease is not null)
            {
                await upstreamLease.DisposeAsync();
            }
        }
    }

    private async ValueTask<ResponseForwardingResult> ForwardHttp1Async(
        Stream upstreamStream,
        Stream clientStream,
        Http1HeadReadResult requestHeadRead,
        Http1RequestHead requestHead,
        RuntimeRoute route,
        RuntimeListener listener,
        RuntimeTimeouts timeouts,
        string upstreamTarget,
        ForwardedHeadersContext forwardedHeaders,
        bool preferClientKeepAlive,
        string requestId,
        bool suppressGeneratedFailureResponse,
        Http1BodyReader? preReadRequestBodyReader,
        byte[]? preReadChunkLine,
        Action markResponseStarted,
        CancellationToken cancellationToken)
    {
        await WriteRequestHeadAsync(upstreamStream, requestHead, route, upstreamTarget, forwardedHeaders, timeouts, cancellationToken);
        await RelayRequestBodyAsync(
            clientStream,
            upstreamStream,
            requestHeadRead.InitialBodyBytes,
            requestHead,
            listener,
            timeouts,
            route.ResolvedOptions.MaxRequestBodyBytes,
            preReadRequestBodyReader,
            preReadChunkLine,
            cancellationToken);

        return await RelayResponseAsync(
            upstreamStream,
            clientStream,
            requestHead,
            route,
            listener,
            timeouts,
            preferClientKeepAlive,
            upstreamTarget,
            requestId,
            suppressGeneratedFailureResponse,
            markResponseStarted,
            cancellationToken);
    }

    private async ValueTask<ResponseForwardingResult> ForwardHttp2Async(
        Stream upstreamStream,
        Stream clientStream,
        Http1HeadReadResult requestHeadRead,
        Http1RequestHead requestHead,
        RuntimeRoute route,
        RuntimeUpstream upstream,
        RuntimeListener listener,
        RuntimeTimeouts timeouts,
        string upstreamTarget,
        ForwardedHeadersContext forwardedHeaders,
        bool preferClientKeepAlive,
        string requestId,
        bool suppressRetryableStatusResponse,
        Http1BodyReader? preReadRequestBodyReader,
        byte[]? preReadChunkLine,
        Action markResponseStarted,
        CancellationToken cancellationToken)
    {
        _metrics.UpstreamHttp2RequestAttempted();
        var upstreamHttp2 = new Http2UpstreamConnection(
            upstreamStream,
            _metrics,
            maxFrameSize: Math.Max(16 * 1024, listener.Http2Limits.MaxFrameSize));
        await upstreamHttp2.InitializeAsync(timeouts, cancellationToken);

        var requestHeaders = BuildHttp2RequestHeaders(requestHead, route, upstream, upstreamTarget, forwardedHeaders);
        var endRequestStream = !Http1RequestFramingPolicy.HasFramedBody(requestHead);
        await upstreamHttp2.SendHeadersAsync(requestHeaders, endRequestStream, timeouts, cancellationToken);
        if (!endRequestStream)
        {
            await RelayFramedUpstreamRequestBodyAsync(
                clientStream,
                requestHeadRead.InitialBodyBytes,
                requestHead,
                listener,
                timeouts,
                route.ResolvedOptions.MaxRequestBodyBytes,
                preReadRequestBodyReader,
                preReadChunkLine,
                upstreamHttp2.SendDataAsync,
                cancellationToken);
        }

        var upstreamResponse = await upstreamHttp2.ReadResponseHeadAsync(
            listener.MaxResponseHeadBytes,
            timeouts,
            cancellationToken);
        var responseHeadTranslation = FramedUpstreamResponsePolicy.BuildHttp1ResponseHead(
            requestHead,
            new FramedUpstreamResponseTranslationInput(
                upstreamResponse.StatusCode,
                upstreamResponse.Headers,
                upstreamResponse.EndStream));
        var responseHead = responseHeadTranslation switch
        {
            FramedUpstreamResponseTranslationResult.AcceptedResult accepted => accepted.ResponseHead,
            FramedUpstreamResponseTranslationResult.RejectedResult rejected => throw new Http2UpstreamProtocolException(
                $"Upstream HTTP/2 response framing was invalid: {rejected.Reason}."),
            _ => throw new InvalidOperationException(
                $"Unexpected upstream response translation result {responseHeadTranslation.GetType().Name}.")
        };

        if (ProxyRetryPolicy.ShouldSuppressRetryableStatusResponse(route.Retry, responseHead.StatusCode, suppressRetryableStatusResponse))
        {
            return CreateRetrySuppressedResult(responseHead.StatusCode);
        }

        var keepClientConnectionOpen = preferClientKeepAlive;
        var responseHeaders = BuildResponseHeaders(responseHead, route);
        if (ProxyCacheEligibilityPolicy.EvaluateResponseForBuffering(ProxyCacheRuntimeMapper.ToPolicyFacts(route.Cache), requestHead, responseHead)
            is ProxyCacheEligibilityResult.AcceptedResult)
        {
            var body = await ReadFramedUpstreamCacheCandidateBodyAsync(
                (readTimeouts, token) => ReadHttp2DataChunkAsync(upstreamHttp2, readTimeouts, token),
                responseHead,
                upstreamResponse.EndStream,
                timeouts,
                cancellationToken);
            await WriteAndStoreBufferedCacheResponseAsync(
                clientStream,
                route,
                listener,
                timeouts,
                requestHead,
                upstreamTarget,
                responseHead,
                responseHeaders,
                body,
                keepClientConnectionOpen,
                requestId,
                markResponseStarted,
                cancellationToken);
        }
        else
        {
            RecordUncacheableFraming(route.Cache, responseHead);
            await WriteResponseHeadAsync(
                clientStream,
                responseHead,
                responseHeaders,
                timeouts,
                keepClientConnectionOpen,
                requestId,
                listener,
                cancellationToken);
            markResponseStarted();
            await RelayHttp2ResponseBodyAsync(
                upstreamHttp2,
                clientStream,
                upstreamResponse.EndStream,
                responseHead,
                timeouts,
                cancellationToken);
        }

        return new ResponseForwardingResult(
            true,
            keepClientConnectionOpen,
            false,
            responseHead.StatusCode);
    }

    private async ValueTask<ResponseForwardingResult> ForwardHttp3Async(
        Stream clientStream,
        Http1HeadReadResult requestHeadRead,
        Http1RequestHead requestHead,
        RuntimeRoute route,
        RuntimeUpstream upstream,
        RuntimeListener listener,
        RuntimeTimeouts timeouts,
        RuntimeConnectionLimits connectionLimits,
        string upstreamTarget,
        ForwardedHeadersContext forwardedHeaders,
        bool preferClientKeepAlive,
        string requestId,
        bool suppressRetryableStatusResponse,
        Http1BodyReader? preReadRequestBodyReader,
        byte[]? preReadChunkLine,
        Action markResponseStarted,
        CancellationToken cancellationToken)
    {
        _metrics.UpstreamHttp3RequestAttempted();
        await using var upstreamHttp3 = await _http3UpstreamConnections.BorrowAsync(
            upstream,
            timeouts,
            connectionLimits,
            Math.Max(16 * 1024, listener.Http2Limits.MaxFrameSize),
            cancellationToken);

        var requestHeaders = BuildHttp2RequestHeaders(requestHead, route, upstream, upstreamTarget, forwardedHeaders);
        var endRequestStream = !Http1RequestFramingPolicy.HasFramedBody(requestHead);
        await upstreamHttp3.SendHeadersAsync(requestHeaders, endRequestStream, timeouts, cancellationToken);
        if (!endRequestStream)
        {
            await RelayFramedUpstreamRequestBodyAsync(
                clientStream,
                requestHeadRead.InitialBodyBytes,
                requestHead,
                listener,
                timeouts,
                route.ResolvedOptions.MaxRequestBodyBytes,
                preReadRequestBodyReader,
                preReadChunkLine,
                upstreamHttp3.SendDataAsync,
                cancellationToken);
        }

        var upstreamResponse = await upstreamHttp3.ReadResponseHeadAsync(
            listener.MaxResponseHeadBytes,
            timeouts,
            cancellationToken);
        var responseHeadTranslation = FramedUpstreamResponsePolicy.BuildHttp1ResponseHead(
            requestHead,
            new FramedUpstreamResponseTranslationInput(
                upstreamResponse.StatusCode,
                upstreamResponse.Headers,
                ResponseEndedWithHead: false));
        var responseHead = responseHeadTranslation switch
        {
            FramedUpstreamResponseTranslationResult.AcceptedResult accepted => accepted.ResponseHead,
            FramedUpstreamResponseTranslationResult.RejectedResult rejected => throw new Http3UpstreamProtocolException(
                $"Upstream HTTP/3 response framing was invalid: {rejected.Reason}."),
            _ => throw new InvalidOperationException(
                $"Unexpected upstream response translation result {responseHeadTranslation.GetType().Name}.")
        };

        if (ProxyRetryPolicy.ShouldSuppressRetryableStatusResponse(route.Retry, responseHead.StatusCode, suppressRetryableStatusResponse))
        {
            return CreateRetrySuppressedResult(responseHead.StatusCode);
        }

        var keepClientConnectionOpen = preferClientKeepAlive;
        var responseHeaders = BuildResponseHeaders(responseHead, route);
        if (ProxyCacheEligibilityPolicy.EvaluateResponseForBuffering(ProxyCacheRuntimeMapper.ToPolicyFacts(route.Cache), requestHead, responseHead)
            is ProxyCacheEligibilityResult.AcceptedResult)
        {
            var body = await ReadFramedUpstreamCacheCandidateBodyAsync(
                (readTimeouts, token) => ReadHttp3DataChunkAsync(upstreamHttp3, readTimeouts, token),
                responseHead,
                endStream: false,
                timeouts,
                cancellationToken);
            await WriteAndStoreBufferedCacheResponseAsync(
                clientStream,
                route,
                listener,
                timeouts,
                requestHead,
                upstreamTarget,
                responseHead,
                responseHeaders,
                body,
                keepClientConnectionOpen,
                requestId,
                markResponseStarted,
                cancellationToken);
        }
        else
        {
            RecordUncacheableFraming(route.Cache, responseHead);
            await WriteResponseHeadAsync(
                clientStream,
                responseHead,
                responseHeaders,
                timeouts,
                keepClientConnectionOpen,
                requestId,
                listener,
                cancellationToken);
            markResponseStarted();
            await RelayHttp3ResponseBodyAsync(
                upstreamHttp3,
                clientStream,
                responseHead,
                timeouts,
                cancellationToken);
        }

        return new ResponseForwardingResult(
            true,
            keepClientConnectionOpen,
            false,
            responseHead.StatusCode);
    }

    private async ValueTask HandleTimeoutAsync(
        Stream clientStream,
        Http1RequestHead requestHead,
        RuntimeUpstream upstream,
        bool responseStarted,
        ProxyTimeoutException exception,
        RuntimeTimeouts timeouts,
        string requestId,
        CancellationToken cancellationToken,
        bool suppressGeneratedFailureResponse)
    {
        switch (exception.Kind)
        {
            case ProxyTimeoutKind.ClientRequestBodyIdle:
                _metrics.ClientRequestBodyTimedOut();
                _logger.LogDebug(exception, "Client request body timed out for {Method} {Target}", requestHead.Method, requestHead.Target);
                if (ProxyGeneratedFailurePolicy.CanWriteFailureResponse(responseStarted, suppressGeneratedFailureResponse))
                {
                    await ProxyGeneratedFailureWriter.WriteAsync(
                        clientStream,
                        ProxyFailureKind.ClientRequestBodyTimeout,
                        timeouts,
                        requestId,
                        _metrics,
                        cancellationToken);
                }
                break;
            case ProxyTimeoutKind.UpstreamConnect:
                _metrics.UpstreamConnectTimedOut();
                _metrics.UpstreamFailed();
                _logger.LogWarning(exception, "Timed out connecting to upstream {UpstreamName}", upstream.Name);
                if (ProxyGeneratedFailurePolicy.CanWriteFailureResponse(responseStarted, suppressGeneratedFailureResponse))
                {
                    ProxyGeneratedFailureMetrics.Record(_metrics, ProxyFailureKind.UpstreamConnectTimeout);
                    await ProxyGeneratedFailureWriter.WriteAsync(
                        clientStream,
                        ProxyFailureKind.UpstreamConnectTimeout,
                        timeouts,
                        requestId,
                        _metrics,
                        cancellationToken);
                }
                break;
            case ProxyTimeoutKind.UpstreamResponseHead:
                _metrics.UpstreamResponseHeadTimedOut();
                _metrics.UpstreamFailed();
                _logger.LogWarning(exception, "Timed out waiting for upstream response head from {UpstreamName}", upstream.Name);
                if (ProxyGeneratedFailurePolicy.CanWriteFailureResponse(responseStarted, suppressGeneratedFailureResponse))
                {
                    ProxyGeneratedFailureMetrics.Record(_metrics, ProxyFailureKind.UpstreamResponseHeadTimeout);
                    await ProxyGeneratedFailureWriter.WriteAsync(
                        clientStream,
                        ProxyFailureKind.UpstreamResponseHeadTimeout,
                        timeouts,
                        requestId,
                        _metrics,
                        cancellationToken);
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

    private IReadOnlyList<ProxyHeaderField> BuildHttp2RequestHeaders(
        Http1RequestHead requestHead,
        RuntimeRoute route,
        RuntimeUpstream upstream,
        string upstreamTarget,
        ForwardedHeadersContext forwardedHeaders)
    {
        var filtered = _headerPolicy.FilterForForwarding(
            requestHead.Headers,
            preserveTransferEncoding: false,
            preserveTrailer: false);
        var requestHeaders = ProxyHeaderMutationPolicy.ApplyRequestHeaders(filtered, route.HeaderPolicy, forwardedHeaders);
        var authority = requestHeaders.FirstOrDefault(static header =>
            string.Equals(header.Name, "Host", StringComparison.OrdinalIgnoreCase))?.Value;
        if (string.IsNullOrWhiteSpace(authority))
        {
            authority = requestHead.Host.Length == 0 ? upstream.Address : requestHead.Host;
        }

        List<ProxyHeaderField> headers =
        [
            new(":method", requestHead.Method),
            new(":scheme", upstream.Scheme),
            new(":authority", authority),
            new(":path", upstreamTarget)
        ];

        foreach (var header in requestHeaders)
        {
            if (Http2HeaderPolicy.IsManagedUpstreamRequestHeader(header.Name))
            {
                continue;
            }

            headers.Add(new ProxyHeaderField(header.Name.ToLowerInvariant(), header.Value));
        }

        if (requestHead.Framing.Kind == Http1BodyKind.ContentLength)
        {
            headers.Add(new ProxyHeaderField(
                "content-length",
                requestHead.Framing.ContentLength.GetValueOrDefault().ToString(CultureInfo.InvariantCulture)));
        }

        return headers;
    }

    private async ValueTask RelayFramedUpstreamRequestBodyAsync(
        Stream clientStream,
        ReadOnlyMemory<byte> initialBodyBytes,
        Http1RequestHead requestHead,
        RuntimeListener listener,
        RuntimeTimeouts timeouts,
        long maxRequestBodyBytes,
        Http1BodyReader? preReadReader,
        byte[]? preReadChunkLine,
        SendFramedUpstreamDataAsync sendDataAsync,
        CancellationToken cancellationToken)
    {
        var reader = preReadReader ?? new Http1BodyReader(clientStream, initialBodyBytes, _metrics, timeouts.ClientRequestBodyIdleTimeout, ProxyTimeoutKind.ClientRequestBodyIdle);
        try
        {
            if (requestHead.Framing.Kind == Http1BodyKind.ContentLength)
            {
                await RelayFixedLengthBodyToFramedUpstreamAsync(
                    reader,
                    sendDataAsync,
                    requestHead.Framing.ContentLength.GetValueOrDefault(),
                    listener.ForwardingBufferBytes,
                    timeouts,
                    cancellationToken);
            }
            else if (requestHead.Framing.Kind == Http1BodyKind.Chunked)
            {
                await RelayChunkedBodyToFramedUpstreamAsync(
                    reader,
                    sendDataAsync,
                    listener,
                    timeouts,
                    preReadChunkLine,
                    maxRequestBodyBytes,
                    cancellationToken);
            }
        }
        catch
        {
            _metrics.ClientBodyRelayFailed();
            throw;
        }
    }

    private static async ValueTask RelayFixedLengthBodyToFramedUpstreamAsync(
        Http1BodyReader reader,
        SendFramedUpstreamDataAsync sendDataAsync,
        long contentLength,
        int bufferSize,
        RuntimeTimeouts timeouts,
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

                remaining -= bytesRead;
                await sendDataAsync(
                    buffer.AsMemory(0, bytesRead),
                    remaining == 0,
                    timeouts,
                    cancellationToken);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async ValueTask RelayChunkedBodyToFramedUpstreamAsync(
        Http1BodyReader reader,
        SendFramedUpstreamDataAsync sendDataAsync,
        RuntimeListener listener,
        RuntimeTimeouts timeouts,
        byte[]? initialChunkLine,
        long maxPayloadBytes,
        CancellationToken cancellationToken)
    {
        var chunkLine = initialChunkLine;
        var relayedPayloadBytes = 0L;
        var buffer = ArrayPool<byte>.Shared.Rent(listener.ForwardingBufferBytes);
        try
        {
            while (true)
            {
                chunkLine ??= await reader.ReadLineWithCrlfAsync(listener.MaxChunkLineBytes, cancellationToken);
                if (!Http1ChunkSizeParser.TryParseLine(chunkLine.AsSpan(), out var chunkSize))
                {
                    throw new Http1ClientProtocolException("Invalid chunk-size line.");
                }

                if (chunkSize == 0)
                {
                    await DiscardTrailerSectionAsync(reader, listener.MaxChunkLineBytes, cancellationToken);
                    await sendDataAsync(ReadOnlyMemory<byte>.Empty, endStream: true, timeouts, cancellationToken);
                    return;
                }

                relayedPayloadBytes += chunkSize;
                if (relayedPayloadBytes > maxPayloadBytes)
                {
                    throw new Http1PayloadTooLargeException("Chunked request body exceeded the configured maximum request body size.");
                }

                var remaining = chunkSize;
                while (remaining > 0)
                {
                    var readLength = (int)Math.Min(buffer.Length, remaining);
                    var bytesRead = await reader.ReadAsync(buffer.AsMemory(0, readLength), cancellationToken);
                    if (bytesRead == 0)
                    {
                        throw new IOException("Source closed before the declared chunk body was complete.");
                    }

                    remaining -= bytesRead;
                    await sendDataAsync(buffer.AsMemory(0, bytesRead), endStream: false, timeouts, cancellationToken);
                }

                var crlf = await reader.ReadExactAsync(2, cancellationToken);
                if (crlf.AsSpan()[0] != (byte)'\r' || crlf.AsSpan()[1] != (byte)'\n')
                {
                    throw new Http1ClientProtocolException("Chunk data was not followed by CRLF.");
                }

                chunkLine = null;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async ValueTask DiscardTrailerSectionAsync(
        Http1BodyReader reader,
        int maxLineBytes,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await reader.ReadLineWithCrlfAsync(maxLineBytes, cancellationToken);
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

    private static async ValueTask<byte[]> ReadFramedUpstreamCacheCandidateBodyAsync(
        ReadFramedUpstreamDataAsync readDataAsync,
        Http1ResponseHead responseHead,
        bool endStream,
        RuntimeTimeouts timeouts,
        CancellationToken cancellationToken)
    {
        if (endStream || responseHead.Framing.Kind == Http1BodyKind.None)
        {
            return [];
        }

        using var body = new MemoryStream();
        while (true)
        {
            var chunk = await readDataAsync(timeouts, cancellationToken);
            if (chunk.Data.Length > 0)
            {
                body.Write(chunk.Data.Span);
            }

            if (chunk.EndStream)
            {
                return body.ToArray();
            }
        }
    }

    private static async ValueTask<FramedUpstreamDataChunk> ReadHttp2DataChunkAsync(
        Http2UpstreamConnection upstreamHttp2,
        RuntimeTimeouts timeouts,
        CancellationToken cancellationToken)
    {
        var chunk = await upstreamHttp2.ReadDataAsync(timeouts, cancellationToken);
        return new FramedUpstreamDataChunk(chunk.Data, chunk.EndStream);
    }

    private static async ValueTask<FramedUpstreamDataChunk> ReadHttp3DataChunkAsync(
        Http3UpstreamConnection upstreamHttp3,
        RuntimeTimeouts timeouts,
        CancellationToken cancellationToken)
    {
        var chunk = await upstreamHttp3.ReadDataAsync(timeouts, cancellationToken);
        return new FramedUpstreamDataChunk(chunk.Data, chunk.EndStream);
    }

    private async ValueTask RelayHttp2ResponseBodyAsync(
        Http2UpstreamConnection upstreamHttp2,
        Stream clientStream,
        bool endStream,
        Http1ResponseHead responseHead,
        RuntimeTimeouts timeouts,
        CancellationToken cancellationToken)
    {
        await RelayFramedUpstreamResponseBodyAsync(
            (readTimeouts, token) => ReadHttp2DataChunkAsync(upstreamHttp2, readTimeouts, token),
            clientStream,
            responseHead,
            endStream,
            timeouts,
            cancellationToken);
    }

    private async ValueTask RelayHttp3ResponseBodyAsync(
        Http3UpstreamConnection upstreamHttp3,
        Stream clientStream,
        Http1ResponseHead responseHead,
        RuntimeTimeouts timeouts,
        CancellationToken cancellationToken)
    {
        await RelayFramedUpstreamResponseBodyAsync(
            (readTimeouts, token) => ReadHttp3DataChunkAsync(upstreamHttp3, readTimeouts, token),
            clientStream,
            responseHead,
            endStream: false,
            timeouts,
            cancellationToken);
    }

    private async ValueTask RelayFramedUpstreamResponseBodyAsync(
        ReadFramedUpstreamDataAsync readDataAsync,
        Stream clientStream,
        Http1ResponseHead responseHead,
        bool endStream,
        RuntimeTimeouts timeouts,
        CancellationToken cancellationToken)
    {
        if (endStream || responseHead.Framing.Kind == Http1BodyKind.None)
        {
            return;
        }

        try
        {
            while (true)
            {
                var chunk = await readDataAsync(timeouts, cancellationToken);
                if (chunk.Data.Length > 0)
                {
                    if (responseHead.Framing.Kind == Http1BodyKind.Chunked)
                    {
                        await WriteHttp1ChunkAsync(clientStream, chunk.Data, timeouts.DownstreamWriteTimeout, cancellationToken);
                    }
                    else
                    {
                        await ProxyTimedStreamWriter.WriteAsync(clientStream, chunk.Data, timeouts.DownstreamWriteTimeout, cancellationToken);
                        _metrics.AddBytesWritten(chunk.Data.Length);
                    }
                }

                if (chunk.EndStream)
                {
                    if (responseHead.Framing.Kind == Http1BodyKind.Chunked)
                    {
                        await ProxyTimedStreamWriter.WriteAsync(clientStream, "0\r\n\r\n"u8.ToArray(), timeouts.DownstreamWriteTimeout, cancellationToken);
                        _metrics.AddBytesWritten(5);
                    }

                    return;
                }
            }
        }
        catch
        {
            _metrics.UpstreamBodyRelayFailed();
            _metrics.UpstreamPrematureDisconnect();
            throw;
        }
    }

    private async ValueTask WriteHttp1ChunkAsync(
        Stream clientStream,
        ReadOnlyMemory<byte> data,
        TimeSpan writeTimeout,
        CancellationToken cancellationToken)
    {
        var prefix = Encoding.ASCII.GetBytes(data.Length.ToString("x", CultureInfo.InvariantCulture) + "\r\n");
        await ProxyTimedStreamWriter.WriteAsync(clientStream, prefix, writeTimeout, cancellationToken);
        await ProxyTimedStreamWriter.WriteAsync(clientStream, data, writeTimeout, cancellationToken);
        await ProxyTimedStreamWriter.WriteAsync(clientStream, "\r\n"u8.ToArray(), writeTimeout, cancellationToken);
        _metrics.AddBytesWritten(prefix.Length + data.Length + 2);
    }

    private async ValueTask WriteRequestHeadAsync(
        Stream upstreamStream,
        Http1RequestHead requestHead,
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
            preserveTrailer: requestHead.Framing.Kind == Http1BodyKind.Chunked);

        var requestHeaders = ProxyHeaderMutationPolicy.ApplyRequestHeaders(filtered, route.HeaderPolicy, forwardedHeaders);
        foreach (var header in requestHeaders)
        {
            if (Http1ManagedHeaderPolicy.IsManagedFramingHeader(header.Name))
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

        builder.Append("Connection: keep-alive\r\n\r\n");
        var bytes = Encoding.ASCII.GetBytes(builder.ToString());
        await ProxyTimedStreamWriter.WriteAsync(upstreamStream, bytes, timeouts.DownstreamWriteTimeout, cancellationToken);
        _metrics.AddBytesWritten(bytes.Length);
    }

    private async ValueTask RelayRequestBodyAsync(
        Stream clientStream,
        Stream upstreamStream,
        ReadOnlyMemory<byte> initialBodyBytes,
        Http1RequestHead requestHead,
        RuntimeListener listener,
        RuntimeTimeouts timeouts,
        long maxRequestBodyBytes,
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
                await RelayChunkedBodyAsync(reader, upstreamStream, listener, timeouts.DownstreamWriteTimeout, preReadChunkLine, maxRequestBodyBytes, cancellationToken);
            }
        }
        catch
        {
            _metrics.ClientBodyRelayFailed();
            throw;
        }
    }

    private async ValueTask<ResponseForwardingResult> RelayResponseAsync(
        Stream upstreamStream,
        Stream clientStream,
        Http1RequestHead requestHead,
        RuntimeRoute route,
        RuntimeListener listener,
        RuntimeTimeouts timeouts,
        bool preferClientKeepAlive,
        string upstreamTarget,
        string requestId,
        bool suppressRetryableStatusResponse,
        Action markResponseStarted,
        CancellationToken cancellationToken)
    {
        ReadOnlyMemory<byte> initialBodyBytes = ReadOnlyMemory<byte>.Empty;
        var responseStarted = false;

        while (true)
        {
            var responseHeadRead = await Http1UpstreamResponseHeadReader.ReadAsync(
                upstreamStream,
                listener.MaxResponseHeadBytes,
                timeouts.UpstreamResponseHeadTimeout,
                _metrics,
                cancellationToken);
            if (!responseHeadRead.HasReadableHead)
            {
                throw new Http1UpstreamProtocolException("Upstream closed before a complete response head was received.");
            }

            if (!Http1ResponseParser.TryParse(
                    responseHeadRead.HeadBytes.Span,
                    requestHead.Method,
                    out var responseHead,
                    out var error))
            {
                throw new Http1UpstreamProtocolException($"Upstream response head was invalid: {error}.");
            }

            var upstreamWantsClose = HopByHopHeaderPolicy.HasConnectionToken(responseHead.Headers, "close");
            var keepClientConnectionOpen = preferClientKeepAlive
                && responseHead.Framing.Kind != Http1BodyKind.CloseDelimited;
            initialBodyBytes = responseHeadRead.InitialBodyBytes;

            if (!Http1ResponseParser.IsInformational(responseHead))
            {
                if (ProxyRetryPolicy.ShouldSuppressRetryableStatusResponse(route.Retry, responseHead.StatusCode, suppressRetryableStatusResponse))
                {
                    return CreateRetrySuppressedResult(responseHead.StatusCode);
                }

                var responseHeaders = BuildResponseHeaders(responseHead, route);
                if (ProxyCacheEligibilityPolicy.EvaluateResponseForBuffering(ProxyCacheRuntimeMapper.ToPolicyFacts(route.Cache), requestHead, responseHead)
                    is ProxyCacheEligibilityResult.AcceptedResult)
                {
                    var body = await ReadCacheCandidateBodyAsync(
                        upstreamStream,
                        initialBodyBytes,
                        responseHead,
                        listener,
                        timeouts,
                        cancellationToken);
                    await WriteAndStoreBufferedCacheResponseAsync(
                        clientStream,
                        route,
                        listener,
                        timeouts,
                        requestHead,
                        upstreamTarget,
                        responseHead,
                        responseHeaders,
                        body,
                        keepClientConnectionOpen,
                        requestId,
                        () =>
                        {
                            responseStarted = true;
                            markResponseStarted();
                        },
                        cancellationToken);
                }
                else
                {
                    RecordUncacheableFraming(route.Cache, responseHead);
                    await WriteResponseHeadAsync(clientStream, responseHead, responseHeaders, timeouts, keepClientConnectionOpen, requestId, listener, cancellationToken);
                    responseStarted = true;
                    markResponseStarted();
                    await RelayResponseBodyAsync(upstreamStream, clientStream, initialBodyBytes, responseHead, listener, timeouts, cancellationToken);
                }

                var canReuseUpstream = !upstreamWantsClose
                    && responseHead.Framing.Kind != Http1BodyKind.CloseDelimited;
                return new ResponseForwardingResult(responseStarted, keepClientConnectionOpen, canReuseUpstream, responseHead.StatusCode);
            }

            var informationalHeaders = BuildResponseHeaders(responseHead, route);
            await WriteResponseHeadAsync(clientStream, responseHead, informationalHeaders, timeouts, keepClientConnectionOpen, requestId, listener, cancellationToken);
            responseStarted = true;
            markResponseStarted();
        }
    }

    private async ValueTask WriteResponseHeadAsync(
        Stream clientStream,
        Http1ResponseHead responseHead,
        IReadOnlyList<ProxyHeaderField> responseHeaders,
        RuntimeTimeouts timeouts,
        bool keepClientConnectionOpen,
        string requestId,
        RuntimeListener listener,
        CancellationToken cancellationToken)
    {
        await Http1ResponseHeadWriter.WriteAsync(
            clientStream,
            responseHead,
            responseHeaders,
            Http3AltSvcPolicy.ApplyHeader([], _altSvcPolicy.CreateHeader(listener)),
            requestId,
            responseHead.Framing.Kind == Http1BodyKind.ContentLength
                ? responseHead.Framing.ContentLength
                : null,
            responseHead.Framing.Kind == Http1BodyKind.Chunked,
            keepClientConnectionOpen,
            timeouts.DownstreamWriteTimeout,
            _metrics,
            cancellationToken);
    }

    private async ValueTask WriteBufferedResponseAsync(
        Stream clientStream,
        Http1ResponseHead responseHead,
        IReadOnlyList<ProxyHeaderField> responseHeaders,
        byte[] body,
        bool keepClientConnectionOpen,
        string requestId,
        RuntimeListener listener,
        RuntimeTimeouts timeouts,
        CancellationToken cancellationToken)
    {
        await Http1ResponseHeadWriter.WriteAsync(
            clientStream,
            responseHead,
            responseHeaders,
            Http3AltSvcPolicy.ApplyHeader([], _altSvcPolicy.CreateHeader(listener)),
            requestId,
            body.Length,
            useChunkedTransferEncoding: false,
            keepClientConnectionOpen,
            timeouts.DownstreamWriteTimeout,
            _metrics,
            cancellationToken);

        if (body.Length > 0)
        {
            await ProxyTimedStreamWriter.WriteAsync(clientStream, body, timeouts.DownstreamWriteTimeout, cancellationToken);
            _metrics.AddBytesWritten(body.Length);
        }
    }

    private async ValueTask WriteAndStoreBufferedCacheResponseAsync(
        Stream clientStream,
        RuntimeRoute route,
        RuntimeListener listener,
        RuntimeTimeouts timeouts,
        Http1RequestHead requestHead,
        string upstreamTarget,
        Http1ResponseHead responseHead,
        IReadOnlyList<ProxyHeaderField> responseHeaders,
        byte[] body,
        bool keepClientConnectionOpen,
        string requestId,
        Action markResponseStarted,
        CancellationToken cancellationToken)
    {
        await WriteBufferedResponseAsync(
            clientStream,
            responseHead,
            responseHeaders,
            body,
            keepClientConnectionOpen,
            requestId,
            listener,
            timeouts,
            cancellationToken);
        markResponseStarted();
        _cacheStore.Store(ProxyCacheRuntimeMapper.ToRequestScope(route, listener), requestHead, upstreamTarget, responseHead, responseHeaders, body);
    }

    private IReadOnlyList<ProxyHeaderField> BuildResponseHeaders(
        Http1ResponseHead responseHead,
        RuntimeRoute route)
    {
        var filtered = _headerPolicy.FilterForForwarding(
            responseHead.Headers,
            preserveTransferEncoding: false,
            preserveTrailer: responseHead.Framing.Kind == Http1BodyKind.Chunked);

        return ProxyHeaderMutationPolicy.ApplyResponseHeaders(filtered, route.HeaderPolicy);
    }

    private static ResponseForwardingResult CreateRetrySuppressedResult(int statusCode)
    {
        return new ResponseForwardingResult(
            false,
            false,
            false,
            statusCode,
            SuppressedForRetry: true);
    }

    private void RecordUncacheableFraming(RuntimeCachePolicy policy, Http1ResponseHead responseHead)
    {
        var policyFacts = ProxyCacheRuntimeMapper.ToPolicyFacts(policy);
        if (ProxyCacheEligibilityPolicy.EvaluateResponseFraming(policyFacts, responseHead)
            is ProxyCacheResponseFramingEligibility.Rejected rejected)
        {
            _cacheStore.RecordUncacheable(policyFacts, rejected.Reason);
        }
    }

    private async ValueTask<byte[]> ReadCacheCandidateBodyAsync(
        Stream upstreamStream,
        ReadOnlyMemory<byte> initialBodyBytes,
        Http1ResponseHead responseHead,
        RuntimeListener listener,
        RuntimeTimeouts timeouts,
        CancellationToken cancellationToken)
    {
        if (responseHead.Framing.Kind == Http1BodyKind.None)
        {
            return [];
        }

        var contentLength = responseHead.Framing.ContentLength.GetValueOrDefault();
        var reader = new Http1BodyReader(upstreamStream, initialBodyBytes, _metrics, timeouts.UpstreamResponseBodyIdleTimeout, ProxyTimeoutKind.UpstreamResponseBodyIdle);
        using var body = new MemoryStream((int)Math.Min(contentLength, int.MaxValue));
        var remaining = contentLength;
        var buffer = ArrayPool<byte>.Shared.Rent(listener.ForwardingBufferBytes);
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

                body.Write(buffer, 0, bytesRead);
                remaining -= bytesRead;
            }

            return body.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async ValueTask RelayResponseBodyAsync(
        Stream upstreamStream,
        Stream clientStream,
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
                await RelayChunkedBodyAsync(reader, clientStream, listener, timeouts.DownstreamWriteTimeout, null, null, cancellationToken);
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

                await ProxyTimedStreamWriter.WriteAsync(destination, buffer.AsMemory(0, bytesRead), writeTimeout, cancellationToken);
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

                await ProxyTimedStreamWriter.WriteAsync(destination, buffer.AsMemory(0, bytesRead), writeTimeout, cancellationToken);
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
        Stream destination,
        RuntimeListener listener,
        TimeSpan writeTimeout,
        byte[]? initialChunkLine,
        long? maxPayloadBytes,
        CancellationToken cancellationToken)
    {
        var chunkLine = initialChunkLine;
        var relayedPayloadBytes = 0L;
        while (true)
        {
            chunkLine ??= await reader.ReadLineWithCrlfAsync(listener.MaxChunkLineBytes, cancellationToken);
            if (!Http1ChunkSizeParser.TryParseLine(chunkLine.AsSpan(), out var chunkSize))
            {
                throw new Http1ClientProtocolException("Invalid chunk-size line.");
            }

            await ProxyTimedStreamWriter.WriteAsync(destination, chunkLine, writeTimeout, cancellationToken);
            _metrics.AddBytesWritten(chunkLine.Length);

            if (chunkSize == 0)
            {
                await RelayTrailerSectionAsync(reader, destination, listener.MaxChunkLineBytes, writeTimeout, cancellationToken);
                return;
            }

            relayedPayloadBytes += chunkSize;
            if (maxPayloadBytes.HasValue && relayedPayloadBytes > maxPayloadBytes.Value)
            {
                throw new Http1PayloadTooLargeException("Chunked request body exceeded the configured maximum request body size.");
            }

            await RelayFixedLengthBodyAsync(reader, destination, chunkSize, listener.ForwardingBufferBytes, writeTimeout, cancellationToken);
            var crlf = await reader.ReadExactAsync(2, cancellationToken);
            if (crlf.AsSpan()[0] != (byte)'\r' || crlf.AsSpan()[1] != (byte)'\n')
            {
                throw new Http1ClientProtocolException("Chunk data was not followed by CRLF.");
            }

            await ProxyTimedStreamWriter.WriteAsync(destination, crlf, writeTimeout, cancellationToken);
            _metrics.AddBytesWritten(crlf.Length);
            chunkLine = null;
        }
    }

    private async ValueTask RelayTrailerSectionAsync(
        Http1BodyReader reader,
        Stream destination,
        int maxLineBytes,
        TimeSpan writeTimeout,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await reader.ReadLineWithCrlfAsync(maxLineBytes, cancellationToken);
            await ProxyTimedStreamWriter.WriteAsync(destination, line, writeTimeout, cancellationToken);
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

    private sealed record ResponseForwardingResult(
        bool ResponseStarted,
        bool KeepClientConnectionOpen,
        bool CanReuseUpstreamConnection,
        int StatusCode,
        bool SuppressedForRetry = false);

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

    private sealed class Http1PayloadTooLargeException : IOException
    {
        public Http1PayloadTooLargeException(string message)
            : base(message)
        {
        }
    }

    private delegate ValueTask SendFramedUpstreamDataAsync(
        ReadOnlyMemory<byte> data,
        bool endStream,
        RuntimeTimeouts timeouts,
        CancellationToken cancellationToken);

    private delegate ValueTask<FramedUpstreamDataChunk> ReadFramedUpstreamDataAsync(
        RuntimeTimeouts timeouts,
        CancellationToken cancellationToken);

    private readonly record struct FramedUpstreamDataChunk(
        ReadOnlyMemory<byte> Data,
        bool EndStream);
}
