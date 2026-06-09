using System.Buffers;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using MDRAVA.API.Proxy.Caching;
using MDRAVA.API.Proxy.Connections;
using MDRAVA.API.Proxy.Http3;
using MDRAVA.API.Proxy.Http2;
using MDRAVA.API.Proxy.Metrics;
using MDRAVA.API.Proxy.Observability;
using MDRAVA.API.Proxy.Protocol;

namespace MDRAVA.API.Proxy.Forwarding;

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
                if (!TryParseChunkSize(preReadChunkLine.AsSpan(), out _))
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
                return new ForwardingResult(
                    false,
                    false,
                    false,
                    responseResult.StatusCode,
                    ProxyFailureKind.UpstreamUnavailable);
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
            return new ForwardingResult(true, responseStarted, responseResult.KeepClientConnectionOpen, responseResult.StatusCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ProxyTimeoutException exception)
        {
            await HandleTimeoutAsync(clientStream, requestHead, upstream, responseStarted, exception, timeouts, requestId, cancellationToken, suppressGeneratedFailureResponse);
            return new ForwardingResult(false, responseStarted, false, StatusCodeForTimeout(exception.Kind, responseStarted), FailureKindForTimeout(exception.Kind));
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

            if (CanWriteGeneratedFailure(responseStarted, suppressGeneratedFailureResponse))
            {
                await ProxyErrorResponses.WriteAsync(
                    clientStream,
                    BuildGeneratedPayloadTooLarge(requestId),
                    timeouts.DownstreamWriteTimeout,
                    _metrics,
                    cancellationToken);
            }

            return new ForwardingResult(false, responseStarted, false, responseStarted ? null : 413, ProxyFailureKind.RequestPayloadTooLarge);
        }
        catch (Http1ClientProtocolException exception)
        {
            _metrics.MalformedRequestRejected();
            _logger.LogDebug(
                exception,
                "Rejected malformed request body for {Method} {Target}",
                requestHead.Method,
                requestHead.Target);

            if (CanWriteGeneratedFailure(responseStarted, suppressGeneratedFailureResponse))
            {
                await ProxyErrorResponses.WriteAsync(
                    clientStream,
                    BuildGeneratedBadRequest(requestId),
                    timeouts.DownstreamWriteTimeout,
                    _metrics,
                    cancellationToken);
            }
            return new ForwardingResult(false, responseStarted, false, responseStarted ? null : 400, ProxyFailureKind.ClientMalformedRequest);
        }
        catch (Http1UpstreamProtocolException exception)
        {
            _metrics.UpstreamMalformedResponse();
            _metrics.UpstreamFailed();
            if (CanWriteGeneratedFailure(responseStarted, suppressGeneratedFailureResponse))
            {
                _metrics.UpstreamConnectFailed();
            }
            _logger.LogWarning(
                exception,
                "Upstream response framing failed for {Method} {Target} to upstream {UpstreamName}",
                requestHead.Method,
                requestHead.Target,
                upstream.Name);

            if (CanWriteGeneratedFailure(responseStarted, suppressGeneratedFailureResponse))
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
        catch (Http2UpstreamProtocolException exception)
        {
            _metrics.UpstreamHttp2ProtocolError("malformed_response");
            _metrics.UpstreamMalformedResponse();
            _metrics.UpstreamFailed();
            if (CanWriteGeneratedFailure(responseStarted, suppressGeneratedFailureResponse))
            {
                _metrics.UpstreamConnectFailed();
            }
            _logger.LogWarning(
                exception,
                "Upstream HTTP/2 response framing failed for {Method} {Target} to upstream {UpstreamName}",
                requestHead.Method,
                requestHead.Target,
                upstream.Name);

            if (CanWriteGeneratedFailure(responseStarted, suppressGeneratedFailureResponse))
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

            if (CanWriteGeneratedFailure(responseStarted, suppressGeneratedFailureResponse))
            {
                _metrics.UpstreamConnectFailed();
            }

            _logger.LogWarning(
                exception,
                "Upstream HTTP/3 forwarding failed for {Method} {Target} to upstream {UpstreamName}",
                requestHead.Method,
                requestHead.Target,
                upstream.Name);

            if (CanWriteGeneratedFailure(responseStarted, suppressGeneratedFailureResponse))
            {
                _metrics.ProxyGenerated502();
                await ProxyErrorResponses.WriteAsync(
                    clientStream,
                    BuildGeneratedBadGateway(requestId),
                    timeouts.DownstreamWriteTimeout,
                    _metrics,
                    cancellationToken);
            }

            return new ForwardingResult(false, responseStarted, false, responseStarted ? null : 502, failureKind);
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

            if (CanWriteGeneratedFailure(responseStarted, suppressGeneratedFailureResponse))
            {
                _metrics.ProxyGenerated502();
                await ProxyErrorResponses.WriteAsync(
                    clientStream,
                    BuildGeneratedBadGateway(requestId),
                    timeouts.DownstreamWriteTimeout,
                    _metrics,
                    cancellationToken);
            }

            return new ForwardingResult(
                false,
                responseStarted,
                false,
                responseStarted ? null : 502,
                responseStarted ? ProxyFailureKind.UpstreamPrematureDisconnect : ProxyFailureKind.UpstreamConnectFailed);
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

            if (CanWriteGeneratedFailure(responseStarted, suppressGeneratedFailureResponse))
            {
                _metrics.ProxyGenerated502();
                await ProxyErrorResponses.WriteAsync(
                    clientStream,
                    BuildGeneratedBadGateway(requestId),
                    timeouts.DownstreamWriteTimeout,
                    _metrics,
                    cancellationToken);
            }
            return new ForwardingResult(
                false,
                responseStarted,
                false,
                responseStarted ? null : 502,
                responseStarted ? ProxyFailureKind.UpstreamPrematureDisconnect : ProxyFailureKind.UpstreamConnectFailed);
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
        var endRequestStream = !HasFramedUpstreamRequestBody(requestHead);
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
        var responseHead = BuildHttp2ResponseHead(requestHead, upstreamResponse);
        if (ShouldSuppressRetryableStatusResponse(route, responseHead, suppressRetryableStatusResponse))
        {
            return CreateRetrySuppressedResult(responseHead.StatusCode);
        }

        var keepClientConnectionOpen = preferClientKeepAlive;
        var responseHeaders = BuildResponseHeaders(responseHead, route);
        if (ShouldBufferForCache(route, requestHead, responseHead))
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
            RecordUncacheableFraming(route, responseHead);
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
                listener,
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
        var endRequestStream = !HasFramedUpstreamRequestBody(requestHead);
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
        var responseHead = BuildHttp3ResponseHead(requestHead, upstreamResponse);
        if (ShouldSuppressRetryableStatusResponse(route, responseHead, suppressRetryableStatusResponse))
        {
            return CreateRetrySuppressedResult(responseHead.StatusCode);
        }

        var keepClientConnectionOpen = preferClientKeepAlive;
        var responseHeaders = BuildResponseHeaders(responseHead, route);
        if (ShouldBufferForCache(route, requestHead, responseHead))
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
            RecordUncacheableFraming(route, responseHead);
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
                listener,
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
                if (CanWriteGeneratedFailure(responseStarted, suppressGeneratedFailureResponse))
                {
                    await ProxyErrorResponses.WriteAsync(clientStream, BuildGeneratedRequestTimeout(requestId), timeouts.DownstreamWriteTimeout, _metrics, cancellationToken);
                }
                break;
            case ProxyTimeoutKind.UpstreamConnect:
                _metrics.UpstreamConnectTimedOut();
                _metrics.UpstreamFailed();
                _logger.LogWarning(exception, "Timed out connecting to upstream {UpstreamName}", upstream.Name);
                if (CanWriteGeneratedFailure(responseStarted, suppressGeneratedFailureResponse))
                {
                    _metrics.ProxyGenerated504();
                    await ProxyErrorResponses.WriteAsync(clientStream, BuildGeneratedGatewayTimeout(requestId), timeouts.DownstreamWriteTimeout, _metrics, cancellationToken);
                }
                break;
            case ProxyTimeoutKind.UpstreamResponseHead:
                _metrics.UpstreamResponseHeadTimedOut();
                _metrics.UpstreamFailed();
                _logger.LogWarning(exception, "Timed out waiting for upstream response head from {UpstreamName}", upstream.Name);
                if (CanWriteGeneratedFailure(responseStarted, suppressGeneratedFailureResponse))
                {
                    _metrics.ProxyGenerated504();
                    await ProxyErrorResponses.WriteAsync(clientStream, BuildGeneratedGatewayTimeout(requestId), timeouts.DownstreamWriteTimeout, _metrics, cancellationToken);
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

    private IReadOnlyList<Http1HeaderField> BuildHttp2RequestHeaders(
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
        var requestHeaders = ApplyRequestHeaderPolicy(filtered, route.HeaderPolicy, forwardedHeaders);
        var authority = requestHeaders.FirstOrDefault(static header =>
            string.Equals(header.Name, "Host", StringComparison.OrdinalIgnoreCase))?.Value;
        if (string.IsNullOrWhiteSpace(authority))
        {
            authority = requestHead.Host.Length == 0 ? upstream.Address : requestHead.Host;
        }

        List<Http1HeaderField> headers =
        [
            new(":method", requestHead.Method),
            new(":scheme", upstream.Scheme),
            new(":authority", authority),
            new(":path", upstreamTarget)
        ];

        foreach (var header in requestHeaders)
        {
            if (IsManagedHttp2RequestHeader(header.Name))
            {
                continue;
            }

            headers.Add(new Http1HeaderField(header.Name.ToLowerInvariant(), header.Value));
        }

        if (requestHead.Framing.Kind == Http1BodyKind.ContentLength)
        {
            headers.Add(new Http1HeaderField(
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

    private static bool HasFramedUpstreamRequestBody(Http1RequestHead requestHead)
    {
        return requestHead.Framing.Kind == Http1BodyKind.Chunked
            || (requestHead.Framing.Kind == Http1BodyKind.ContentLength
                && requestHead.Framing.ContentLength.GetValueOrDefault() > 0);
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
                if (!TryParseChunkSize(chunkLine.AsSpan(), out var chunkSize))
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

    private static Http1ResponseHead BuildHttp2ResponseHead(
        Http1RequestHead requestHead,
        Http2UpstreamResponseHead upstreamResponse)
    {
        var noBody = upstreamResponse.EndStream
            || string.Equals(requestHead.Method, "HEAD", StringComparison.OrdinalIgnoreCase)
            || upstreamResponse.StatusCode is 204 or 304;
        var framing = noBody
            ? Http1ResponseFraming.None
            : TryGetContentLength(upstreamResponse.Headers, out var contentLength)
                ? Http1ResponseFraming.FromContentLength(contentLength)
                : Http1ResponseFraming.Chunked;
        return new Http1ResponseHead(
            "HTTP/1.1",
            upstreamResponse.StatusCode,
            ProxyRouteActionPolicy.ReasonPhrase(upstreamResponse.StatusCode),
            framing,
            upstreamResponse.Headers);
    }

    private static Http1ResponseHead BuildHttp3ResponseHead(
        Http1RequestHead requestHead,
        Http3UpstreamResponseHead upstreamResponse)
    {
        var noBody = string.Equals(requestHead.Method, "HEAD", StringComparison.OrdinalIgnoreCase)
            || upstreamResponse.StatusCode is 204 or 304;
        var framing = noBody
            ? Http1ResponseFraming.None
            : TryGetContentLength(upstreamResponse.Headers, out var contentLength)
                ? Http1ResponseFraming.FromContentLength(contentLength)
                : Http1ResponseFraming.Chunked;
        return new Http1ResponseHead(
            "HTTP/1.1",
            upstreamResponse.StatusCode,
            ProxyRouteActionPolicy.ReasonPhrase(upstreamResponse.StatusCode),
            framing,
            upstreamResponse.Headers);
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
        RuntimeListener listener,
        RuntimeTimeouts timeouts,
        CancellationToken cancellationToken)
    {
        _ = listener;
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
        RuntimeListener listener,
        RuntimeTimeouts timeouts,
        CancellationToken cancellationToken)
    {
        _ = listener;
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
                        await WriteWithTimeoutAsync(clientStream, chunk.Data, timeouts.DownstreamWriteTimeout, cancellationToken);
                        _metrics.AddBytesWritten(chunk.Data.Length);
                    }
                }

                if (chunk.EndStream)
                {
                    if (responseHead.Framing.Kind == Http1BodyKind.Chunked)
                    {
                        await WriteWithTimeoutAsync(clientStream, "0\r\n\r\n"u8.ToArray(), timeouts.DownstreamWriteTimeout, cancellationToken);
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
        await WriteWithTimeoutAsync(clientStream, prefix, writeTimeout, cancellationToken);
        await WriteWithTimeoutAsync(clientStream, data, writeTimeout, cancellationToken);
        await WriteWithTimeoutAsync(clientStream, "\r\n"u8.ToArray(), writeTimeout, cancellationToken);
        _metrics.AddBytesWritten(prefix.Length + data.Length + 2);
    }

    private static bool IsManagedHttp2RequestHeader(string headerName)
    {
        return headerName.StartsWith(':')
            || string.Equals(headerName, "Host", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "Connection", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "Content-Length", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "Upgrade", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "Keep-Alive", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "Proxy-Connection", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "X-Request-Id", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetContentLength(
        IReadOnlyList<Http1HeaderField> headers,
        out long contentLength)
    {
        contentLength = 0;
        foreach (var header in headers)
        {
            if (!string.Equals(header.Name, "content-length", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return long.TryParse(header.Value, NumberStyles.None, CultureInfo.InvariantCulture, out contentLength)
                && contentLength >= 0;
        }

        return false;
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

        var requestHeaders = ApplyRequestHeaderPolicy(filtered, route.HeaderPolicy, forwardedHeaders);
        foreach (var header in requestHeaders)
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

        builder.Append("Connection: keep-alive\r\n\r\n");
        var bytes = Encoding.ASCII.GetBytes(builder.ToString());
        await WriteWithTimeoutAsync(upstreamStream, bytes, timeouts.DownstreamWriteTimeout, cancellationToken);
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
            var responseHeadRead = await ReadResponseHeadAsync(upstreamStream, listener.MaxResponseHeadBytes, timeouts.UpstreamResponseHeadTimeout, cancellationToken);
            if (responseHeadRead.HeadLength <= 0)
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

            var upstreamWantsClose = HasConnectionToken(responseHead.Headers, "close");
            var keepClientConnectionOpen = preferClientKeepAlive
                && responseHead.Framing.Kind != Http1BodyKind.CloseDelimited;
            initialBodyBytes = responseHeadRead.InitialBodyBytes;

            if (!Http1ResponseParser.IsInformational(responseHead))
            {
                if (ShouldSuppressRetryableStatusResponse(route, responseHead, suppressRetryableStatusResponse))
                {
                    return CreateRetrySuppressedResult(responseHead.StatusCode);
                }

                var responseHeaders = BuildResponseHeaders(responseHead, route);
                if (ShouldBufferForCache(route, requestHead, responseHead))
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
                    RecordUncacheableFraming(route, responseHead);
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
        IReadOnlyList<Http1HeaderField> responseHeaders,
        RuntimeTimeouts timeouts,
        bool keepClientConnectionOpen,
        string requestId,
        RuntimeListener listener,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.Append(responseHead.Version).Append(' ')
            .Append(responseHead.StatusCode).Append(' ')
            .Append(responseHead.ReasonPhrase).Append("\r\n");

        foreach (var header in responseHeaders)
        {
            if (IsManagedFramingHeader(header.Name))
            {
                continue;
            }

            builder.Append(header.Name).Append(": ").Append(header.Value).Append("\r\n");
        }

        if (_altSvcPolicy.TryCreateHeader(listener, out var altSvc))
        {
            builder.Append(altSvc.Name).Append(": ").Append(altSvc.Value).Append("\r\n");
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

        builder.Append(keepClientConnectionOpen ? "Connection: keep-alive\r\n\r\n" : "Connection: close\r\n\r\n");
        var bytes = Encoding.ASCII.GetBytes(builder.ToString());
        await WriteWithTimeoutAsync(clientStream, bytes, timeouts.DownstreamWriteTimeout, cancellationToken);
        _metrics.AddBytesWritten(bytes.Length);
    }

    private async ValueTask WriteBufferedResponseAsync(
        Stream clientStream,
        Http1ResponseHead responseHead,
        IReadOnlyList<Http1HeaderField> responseHeaders,
        byte[] body,
        bool keepClientConnectionOpen,
        string requestId,
        RuntimeListener listener,
        RuntimeTimeouts timeouts,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.Append(responseHead.Version).Append(' ')
            .Append(responseHead.StatusCode).Append(' ')
            .Append(responseHead.ReasonPhrase).Append("\r\n");

        foreach (var header in responseHeaders)
        {
            if (IsManagedFramingHeader(header.Name))
            {
                continue;
            }

            builder.Append(header.Name).Append(": ").Append(header.Value).Append("\r\n");
        }

        if (_altSvcPolicy.TryCreateHeader(listener, out var altSvc))
        {
            builder.Append(altSvc.Name).Append(": ").Append(altSvc.Value).Append("\r\n");
        }

        builder.Append("X-Request-Id: ").Append(requestId).Append("\r\n");
        builder.Append("Content-Length: ").Append(body.Length).Append("\r\n");
        builder.Append(keepClientConnectionOpen ? "Connection: keep-alive\r\n\r\n" : "Connection: close\r\n\r\n");
        var bytes = Encoding.ASCII.GetBytes(builder.ToString());
        await WriteWithTimeoutAsync(clientStream, bytes, timeouts.DownstreamWriteTimeout, cancellationToken);
        _metrics.AddBytesWritten(bytes.Length);

        if (body.Length > 0)
        {
            await WriteWithTimeoutAsync(clientStream, body, timeouts.DownstreamWriteTimeout, cancellationToken);
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
        IReadOnlyList<Http1HeaderField> responseHeaders,
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
        _cacheStore.Store(route, listener, requestHead, upstreamTarget, responseHead, responseHeaders, body);
    }

    private IReadOnlyList<Http1HeaderField> BuildResponseHeaders(
        Http1ResponseHead responseHead,
        RuntimeRoute route)
    {
        var filtered = _headerPolicy.FilterForForwarding(
            responseHead.Headers,
            preserveTransferEncoding: false,
            preserveTrailer: responseHead.Framing.Kind == Http1BodyKind.Chunked);

        return ApplyResponseHeaderPolicy(filtered, route.HeaderPolicy);
    }

    private static bool ShouldSuppressRetryableStatusResponse(
        RuntimeRoute route,
        Http1ResponseHead responseHead,
        bool suppressRetryableStatusResponse)
    {
        return suppressRetryableStatusResponse
            && ContainsStatus(route.Retry.RetryOnStatusCodes, responseHead.StatusCode);
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

    private static bool ShouldBufferForCache(
        RuntimeRoute route,
        Http1RequestHead requestHead,
        Http1ResponseHead responseHead)
    {
        if (!route.Cache.Enabled
            || !ContainsMethod(route.Cache.Methods, requestHead.Method)
            || requestHead.Framing.Kind != Http1BodyKind.None
            || ContainsHeader(requestHead.Headers, "Authorization")
            || !ContainsStatus(route.Cache.CacheableStatusCodes, responseHead.StatusCode)
            || ContainsHeader(responseHead.Headers, "Set-Cookie")
            || (route.Cache.RespectOriginCacheControl && HasUncacheableCacheControl(responseHead.Headers)))
        {
            return false;
        }

        if (responseHead.Framing.Kind == Http1BodyKind.None)
        {
            return true;
        }

        return responseHead.Framing.Kind == Http1BodyKind.ContentLength
            && responseHead.Framing.ContentLength.GetValueOrDefault() <= route.Cache.MaxEntryBytes;
    }

    private void RecordUncacheableFraming(RuntimeRoute route, Http1ResponseHead responseHead)
    {
        if (!route.Cache.Enabled)
        {
            return;
        }

        if (responseHead.Framing.Kind is Http1BodyKind.Chunked or Http1BodyKind.CloseDelimited)
        {
            _cacheStore.RecordUncacheable(route, "framing");
        }
        else if (responseHead.Framing.Kind == Http1BodyKind.ContentLength
            && responseHead.Framing.ContentLength.GetValueOrDefault() > route.Cache.MaxEntryBytes)
        {
            _cacheStore.RecordUncacheable(route, "oversized");
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

            await WriteWithTimeoutAsync(destination, crlf, writeTimeout, cancellationToken);
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
            || string.Equals(headerName, "Connection", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "X-Request-Id", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<Http1HeaderField> ApplyRequestHeaderPolicy(
        IReadOnlyList<Http1HeaderField> headers,
        RuntimeHeaderPolicy policy,
        ForwardedHeadersContext forwardedHeaders)
    {
        var result = headers
            .Where(header => !ForwardedHeadersPolicy.IsForwardedHeader(header.Name))
            .Where(header => !ContainsHeaderName(policy.RemoveRequestHeaders, header.Name))
            .Where(header => !ContainsHeaderName(policy.SetRequestHeaders.Select(static set => set.Name), header.Name))
            .ToList();

        result.AddRange(policy.SetRequestHeaders);
        foreach (var forwardedHeader in forwardedHeaders.Headers)
        {
            result.RemoveAll(header => string.Equals(header.Name, forwardedHeader.Name, StringComparison.OrdinalIgnoreCase));
            result.Add(forwardedHeader);
        }

        return result;
    }

    private static IReadOnlyList<Http1HeaderField> ApplyResponseHeaderPolicy(
        IReadOnlyList<Http1HeaderField> headers,
        RuntimeHeaderPolicy policy)
    {
        var result = headers
            .Where(header => !ContainsHeaderName(policy.RemoveResponseHeaders, header.Name))
            .Where(header => !ContainsHeaderName(policy.SetResponseHeaders.Select(static set => set.Name), header.Name))
            .ToList();

        result.AddRange(policy.SetResponseHeaders);
        return result;
    }

    private static bool ContainsHeaderName(IEnumerable<string> headerNames, string headerName)
    {
        return headerNames.Any(name => string.Equals(name, headerName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsHeader(IReadOnlyList<Http1HeaderField> headers, string headerName)
    {
        return headers.Any(header => string.Equals(header.Name, headerName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsStatus(IReadOnlyList<int> statusCodes, int statusCode)
    {
        return statusCodes.Any(code => code == statusCode);
    }

    private static bool ContainsMethod(IReadOnlyList<string> methods, string method)
    {
        return methods.Any(value => string.Equals(value, method, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasUncacheableCacheControl(IReadOnlyList<Http1HeaderField> headers)
    {
        foreach (var header in headers)
        {
            if (!string.Equals(header.Name, "Cache-Control", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var directive in header.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var name = directive.Split('=', 2)[0].Trim();
                if (string.Equals(name, "no-store", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "private", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "no-cache", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "must-revalidate", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static ReadOnlyMemory<byte> BuildGeneratedBadRequest(string requestId)
    {
        return Encoding.ASCII.GetBytes(
            $"HTTP/1.1 400 Bad Request\r\nConnection: close\r\nContent-Length: 11\r\nContent-Type: text/plain\r\nX-Request-Id: {requestId}\r\n\r\nBad Request");
    }

    private static ReadOnlyMemory<byte> BuildGeneratedBadGateway(string requestId)
    {
        return Encoding.ASCII.GetBytes(
            $"HTTP/1.1 502 Bad Gateway\r\nConnection: close\r\nContent-Length: 11\r\nContent-Type: text/plain\r\nX-Request-Id: {requestId}\r\n\r\nBad Gateway");
    }

    private static ReadOnlyMemory<byte> BuildGeneratedRequestTimeout(string requestId)
    {
        return Encoding.ASCII.GetBytes(
            $"HTTP/1.1 408 Request Timeout\r\nConnection: close\r\nContent-Length: 15\r\nContent-Type: text/plain\r\nX-Request-Id: {requestId}\r\n\r\nRequest Timeout");
    }

    private static ReadOnlyMemory<byte> BuildGeneratedGatewayTimeout(string requestId)
    {
        return Encoding.ASCII.GetBytes(
            $"HTTP/1.1 504 Gateway Timeout\r\nConnection: close\r\nContent-Length: 15\r\nContent-Type: text/plain\r\nX-Request-Id: {requestId}\r\n\r\nGateway Timeout");
    }

    private static ReadOnlyMemory<byte> BuildGeneratedPayloadTooLarge(string requestId)
    {
        return Encoding.ASCII.GetBytes(
            $"HTTP/1.1 413 Payload Too Large\r\nConnection: close\r\nContent-Length: 17\r\nContent-Type: text/plain\r\nX-Request-Id: {requestId}\r\n\r\nPayload Too Large");
    }

    private static int? StatusCodeForTimeout(ProxyTimeoutKind timeoutKind, bool responseStarted)
    {
        if (responseStarted)
        {
            return null;
        }

        return timeoutKind switch
        {
            ProxyTimeoutKind.ClientRequestBodyIdle => 408,
            ProxyTimeoutKind.UpstreamConnect => 504,
            ProxyTimeoutKind.UpstreamResponseHead => 504,
            _ => null
        };
    }

    private static bool CanWriteGeneratedFailure(bool responseStarted, bool suppressGeneratedFailureResponse)
    {
        return !responseStarted && !suppressGeneratedFailureResponse;
    }

    private static ProxyFailureKind FailureKindForTimeout(ProxyTimeoutKind timeoutKind)
    {
        return timeoutKind switch
        {
            ProxyTimeoutKind.ClientRequestBodyIdle => ProxyFailureKind.ClientRequestBodyTimeout,
            ProxyTimeoutKind.UpstreamConnect => ProxyFailureKind.UpstreamConnectTimeout,
            ProxyTimeoutKind.UpstreamResponseHead => ProxyFailureKind.UpstreamResponseHeadTimeout,
            ProxyTimeoutKind.UpstreamResponseBodyIdle => ProxyFailureKind.UpstreamResponseBodyTimeout,
            ProxyTimeoutKind.DownstreamWrite => ProxyFailureKind.ClientDisconnected,
            _ => ProxyFailureKind.InternalError
        };
    }

    public static bool HasConnectionToken(IReadOnlyList<Http1HeaderField> headers, string token)
    {
        foreach (var header in headers)
        {
            if (!string.Equals(header.Name, "Connection", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var value in header.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.Equals(value, token, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
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
        private readonly Stream _stream;
        private readonly ProxyMetrics _metrics;
        private readonly TimeSpan _readTimeout;
        private readonly ProxyTimeoutKind _timeoutKind;
        private ReadOnlyMemory<byte> _initialBytes;

        public Http1BodyReader(
            Stream stream,
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
