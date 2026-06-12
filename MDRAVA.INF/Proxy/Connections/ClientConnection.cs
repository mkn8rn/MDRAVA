using MDRAVA.BLL.ControlPlane.Headers;
using MDRAVA.BLL.ControlPlane.Forwarding;
using MDRAVA.BLL.ControlPlane.Http1;
using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.ControlPlane.Routing;
using MDRAVA.BLL.ControlPlane.RuntimeGuards;
using MDRAVA.BLL.ControlPlane.Timeouts;
using MDRAVA.BLL.ControlPlane.UpstreamSelection;
using MDRAVA.BLL.ControlPlane.Upgrades;
using MDRAVA.BLL.ControlPlane.Resilience;
using MDRAVA.BLL.ControlPlane.HealthChecks;
using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Metrics;
using MDRAVA.BLL.ControlPlane.RequestDiagnostics;
using MDRAVA.BLL.Http;
using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.Caching;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Net.Sockets;
using System.Net.Security;
using System.Text;
using MDRAVA.INF.Proxy.Forwarding;
using MDRAVA.INF.Proxy.Health;
using MDRAVA.INF.Proxy.Http2;
using MDRAVA.INF.Proxy.RuntimeGuards;
using MDRAVA.INF.Proxy.Http3;
using MDRAVA.INF.Observability;
using MDRAVA.INF.Proxy.Tls;
using System.Net;

namespace MDRAVA.INF.Proxy.Connections;

public sealed class ClientConnection
{
    private const int EmptyRequestHead = 0;
    private const int RequestHeadTooLarge = -1;
    private const int IncompleteRequestHead = -2;

    private readonly Socket _socket;
    private readonly ProxyConfigurationSnapshot _configurationSnapshot;
    private readonly RuntimeListener _listener;
    private readonly IRouteMatcher _routeMatcher;
    private readonly IUpstreamSelector _upstreamSelector;
    private readonly UpstreamHealthStore _healthStore;
    private readonly ProxyForwarder _forwarder;
    private readonly UpgradeForwarder _upgradeForwarder;
    private readonly UpgradeRequestPolicy _upgradeRequestPolicy;
    private readonly ForwardedHeadersPolicy _forwardedHeadersPolicy;
    private readonly ProxyRouteActionPolicy _routeActionPolicy;
    private readonly PathRewritePolicy _pathRewritePolicy;
    private readonly ResponseCacheStore _cacheStore;
    private readonly Http3AltSvcPolicy _altSvcPolicy;
    private readonly CircuitBreakerStore _circuitBreakerStore;
    private readonly AcmeHttp01ChallengeResponder _acmeChallengeResponder;
    private readonly TlsConnectionAuthenticator _tlsAuthenticator;
    private readonly ProxyMetrics _metrics;
    private readonly RequestIdGenerator _requestIdGenerator;
    private readonly AccessLogEmitter _accessLogEmitter;
    private readonly ClientRateLimiter _rateLimiter;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ClientConnection> _logger;

    public ClientConnection(
        Socket socket,
        ProxyConfigurationSnapshot configurationSnapshot,
        RuntimeListener listener,
        IRouteMatcher routeMatcher,
        IUpstreamSelector upstreamSelector,
        UpstreamHealthStore healthStore,
        ProxyForwarder forwarder,
        UpgradeForwarder upgradeForwarder,
        UpgradeRequestPolicy upgradeRequestPolicy,
        ForwardedHeadersPolicy forwardedHeadersPolicy,
        ProxyRouteActionPolicy routeActionPolicy,
        PathRewritePolicy pathRewritePolicy,
        ResponseCacheStore cacheStore,
        Http3AltSvcPolicy altSvcPolicy,
        CircuitBreakerStore circuitBreakerStore,
        AcmeHttp01ChallengeResponder acmeChallengeResponder,
        TlsConnectionAuthenticator tlsAuthenticator,
        ProxyMetrics metrics,
        RequestIdGenerator requestIdGenerator,
        AccessLogEmitter accessLogEmitter,
        ClientRateLimiter rateLimiter,
        TimeProvider timeProvider,
        ILogger<ClientConnection> logger)
    {
        _socket = socket;
        _configurationSnapshot = configurationSnapshot;
        _listener = listener;
        _routeMatcher = routeMatcher;
        _upstreamSelector = upstreamSelector;
        _healthStore = healthStore;
        _forwarder = forwarder;
        _upgradeForwarder = upgradeForwarder;
        _upgradeRequestPolicy = upgradeRequestPolicy;
        _forwardedHeadersPolicy = forwardedHeadersPolicy;
        _routeActionPolicy = routeActionPolicy;
        _pathRewritePolicy = pathRewritePolicy;
        _cacheStore = cacheStore;
        _altSvcPolicy = altSvcPolicy;
        _circuitBreakerStore = circuitBreakerStore;
        _acmeChallengeResponder = acmeChallengeResponder;
        _tlsAuthenticator = tlsAuthenticator;
        _metrics = metrics;
        _requestIdGenerator = requestIdGenerator;
        _accessLogEmitter = accessLogEmitter;
        _rateLimiter = rateLimiter;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async ValueTask RunAsync(CancellationToken cancellationToken)
    {
        _socket.NoDelay = true;

        var transportStream = new NetworkStream(_socket, ownsSocket: true);
        Stream clientStream = transportStream;
        TlsAuthenticationResult? tlsResult = null;
        if (_listener.Transport == RuntimeListenerTransport.Https)
        {
            tlsResult = await _tlsAuthenticator.AuthenticateAsync(
                transportStream,
                _configurationSnapshot,
                _listener,
                cancellationToken);
            if (tlsResult is null)
            {
                return;
            }

            clientStream = tlsResult.Stream;
        }

        await using var ownedClientStream = clientStream;
        if (tlsResult?.NegotiatedProtocol == SslApplicationProtocol.Http2)
        {
            var http2Connection = new Http2ClientConnection(
                clientStream,
                GetRemoteEndPoint(),
                _configurationSnapshot,
                _listener,
                _routeMatcher,
                _upstreamSelector,
                _healthStore,
                _forwarder,
                _forwardedHeadersPolicy,
                _routeActionPolicy,
                _pathRewritePolicy,
                _cacheStore,
                _altSvcPolicy,
                _circuitBreakerStore,
                _acmeChallengeResponder,
                _metrics,
                _requestIdGenerator,
                _accessLogEmitter,
                _rateLimiter,
                _timeProvider,
                _logger);
            await http2Connection.RunAsync(cancellationToken);
            return;
        }

        if (!_listener.Protocols.HasFlag(RuntimeListenerProtocols.Http1))
        {
            return;
        }

        var maxRequestHeadBytes = Math.Min(_listener.MaxRequestHeadBytes, _configurationSnapshot.Limits.MaxRequestHeadBytes);
        var requestHeadBuffer = ArrayPool<byte>.Shared.Rent(maxRequestHeadBytes);
        var requestsProcessed = 0;
        ProxyRequestContext? currentContext = null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                currentContext = requestsProcessed == 0 ? CreateRequestContext() : null;
                var timeoutKind = requestsProcessed == 0
                    ? ProxyTimeoutKind.ClientRequestHead
                    : ProxyTimeoutKind.ClientKeepAliveIdle;
                var timeout = requestsProcessed == 0
                    ? _configurationSnapshot.Timeouts.ClientRequestHeadTimeout
                    : _configurationSnapshot.Timeouts.ClientKeepAliveIdleTimeout;
                var requestHeadRead = await ProxyTimeoutPolicy.RunAsync(
                    timeoutToken => ReadRequestHeadAsync(clientStream, requestHeadBuffer, maxRequestHeadBytes, timeoutToken),
                    timeout,
                    timeoutKind,
                    cancellationToken);
                if (requestHeadRead.HeadLength == EmptyRequestHead)
                {
                    return;
                }

                currentContext ??= CreateRequestContext();

                if (requestHeadRead.HeadLength == RequestHeadTooLarge)
                {
                    _metrics.ParseFailed();
                    _metrics.ParserLimitRejected();
                    _metrics.MalformedRequestRejected();
                    await WriteGeneratedResponseAsync(
                        clientStream,
                        431,
                        "Request Header Fields Too Large",
                        "Request Head Too Large",
                        currentContext,
                        ProxyFailureKind.ParserLimitExceeded,
                        cancellationToken);
                    CompleteContext(ref currentContext);
                    return;
                }

                if (requestHeadRead.HeadLength == IncompleteRequestHead)
                {
                    _metrics.ParseFailed();
                    _metrics.MalformedRequestRejected();
                    await WriteGeneratedResponseAsync(
                        clientStream,
                        400,
                        "Bad Request",
                        "Bad Request",
                        currentContext,
                        ProxyFailureKind.ClientMalformedRequest,
                        cancellationToken);
                    CompleteContext(ref currentContext);
                    return;
                }

                var requestHeadBytes = requestHeadRead.HeadBytes;
                if (!Http1RequestParser.TryParse(
                        requestHeadBytes.Span,
                        new Http1RequestParseLimits(
                            _configurationSnapshot.Limits.MaxHeaderCount,
                            _configurationSnapshot.Limits.MaxHeaderLineBytes,
                            _configurationSnapshot.Limits.MaxPathBytes),
                        out var requestHead,
                        out var parseError))
                {
                    _metrics.ParseFailed();
                    if (parseError is Http1ParseError.HeaderCountExceeded or Http1ParseError.HeaderLineTooLarge or Http1ParseError.TargetTooLarge)
                    {
                        _metrics.ParserLimitRejected();
                        await WriteGeneratedResponseAsync(
                            clientStream,
                            431,
                            "Request Header Fields Too Large",
                            "Request Head Too Large",
                            currentContext,
                            ProxyFailureKind.ParserLimitExceeded,
                            cancellationToken);
                        CompleteContext(ref currentContext);
                        return;
                    }

                    if (parseError == Http1ParseError.UnsupportedTransferEncoding)
                    {
                        _metrics.UnsupportedRequestFramingRejected();
                        await WriteGeneratedResponseAsync(
                            clientStream,
                            501,
                            "Not Implemented",
                            "Not Implemented",
                            currentContext,
                            ProxyFailureKind.ClientMalformedRequest,
                            cancellationToken);
                        CompleteContext(ref currentContext);
                        return;
                    }

                    _metrics.MalformedRequestRejected();
                    _logger.LogDebug("Rejected malformed request head with parse error {ParseError}", parseError);
                    await WriteGeneratedResponseAsync(
                        clientStream,
                        400,
                        "Bad Request",
                        "Bad Request",
                        currentContext,
                        ProxyFailureKind.ClientMalformedRequest,
                        cancellationToken);
                    CompleteContext(ref currentContext);
                    return;
                }

                _metrics.RequestReceived();
                currentContext.SetRequest(
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
                    ProxyClientAddressPolicy.ToForwardedHeadersPeer(GetRemoteEndPoint()));
                currentContext.SetClientEndpoint(forwardedHeaders.ResolvedClientEndpoint);

                if (!_rateLimiter.TryAcquireRequest(
                    forwardedHeaders.ResolvedClientAddress,
                    _configurationSnapshot.Limits.RequestsPerMinutePerIp))
                {
                    await WriteGeneratedResponseAsync(
                        clientStream,
                        429,
                        "Too Many Requests",
                        "Too Many Requests",
                        currentContext,
                        ProxyFailureKind.RateLimited,
                        cancellationToken);
                    CompleteContext(ref currentContext);
                    return;
                }

                if (ProxyRequestMethodPolicy.IsConnectTunnelMethod(requestHead.Method))
                {
                    _metrics.UnsupportedRequestFramingRejected();
                    await WriteGeneratedResponseAsync(
                        clientStream,
                        501,
                        "Not Implemented",
                        "Not Implemented",
                        currentContext,
                        ProxyFailureKind.ClientMalformedRequest,
                        cancellationToken);
                    CompleteContext(ref currentContext);
                    return;
                }

                if (_acmeChallengeResponder.TryCreateResponse(requestHead, out var acmeChallengeResponse))
                {
                    await WriteGeneratedRouteResponseAsync(
                        clientStream,
                        acmeChallengeResponse,
                        currentContext,
                        cancellationToken);
                    CompleteContext(ref currentContext);
                    return;
                }

                if (_upgradeRequestPolicy.IsUpgradeRequest(requestHead))
                {
                    var shouldContinue = await HandleUpgradeAsync(
                        clientStream,
                        requestHead,
                        forwardedHeaders,
                        currentContext,
                        cancellationToken);
                    CompleteContext(ref currentContext);
                    if (!shouldContinue)
                    {
                        return;
                    }

                    continue;
                }

                var routeMatch = _routeMatcher.Match(_configurationSnapshot, requestHead);
                if (routeMatch is null)
                {
                    await WriteGeneratedResponseAsync(
                        clientStream,
                        404,
                        "Not Found",
                        "Not Found",
                        currentContext,
                        ProxyFailureKind.NoMatchingRoute,
                        cancellationToken);
                    CompleteContext(ref currentContext);
                    return;
                }
                currentContext.SetRoute(ProxyRequestContextRuntimeMapper.ToRequestRoute(routeMatch.Route));

                if (await TryHandleGeneratedRouteActionAsync(
                        clientStream,
                        routeMatch.Route,
                        requestHead,
                        currentContext,
                        cancellationToken))
                {
                    CompleteContext(ref currentContext);
                    return;
                }

                if (await TryRejectKnownLengthRequestBodyAsync(
                        clientStream,
                        routeMatch.Route,
                        requestHead,
                        currentContext,
                        cancellationToken))
                {
                    CompleteContext(ref currentContext);
                    return;
                }

                var nextRequestCount = requestsProcessed + 1;
                var preferKeepAlive = Http1ClientConnectionPolicy.ShouldKeepOpen(requestHead)
                    && nextRequestCount < _configurationSnapshot.ConnectionLimits.MaxRequestsPerClientConnection;
                var upstreamTarget = _pathRewritePolicy.Apply(routeMatch.Route, requestHead.Target, requestHead.Path);
                var effectiveTimeouts = ProxyTimeoutPolicy.ApplyRouteTimeouts(routeMatch.Route, _configurationSnapshot.Timeouts);

                if (await TryHandleCacheHitAsync(
                        clientStream,
                        routeMatch.Route,
                        requestHead,
                        upstreamTarget,
                        preferKeepAlive,
                        currentContext,
                        effectiveTimeouts,
                        cancellationToken))
                {
                    requestsProcessed = nextRequestCount;
                    if (requestsProcessed >= _configurationSnapshot.ConnectionLimits.MaxRequestsPerClientConnection)
                    {
                        _metrics.ClientConnectionClosedByMaxRequests();
                        currentContext.KeepClientConnectionOpen = false;
                        CompleteContext(ref currentContext);
                        return;
                    }

                    CompleteContext(ref currentContext);
                    if (!preferKeepAlive)
                    {
                        return;
                    }

                    continue;
                }

                var result = await ForwardWithRetriesAsync(
                    clientStream,
                    requestHeadRead,
                    requestHead,
                    routeMatch.Route,
                    _listener,
                    effectiveTimeouts,
                    _configurationSnapshot.ConnectionLimits,
                    _configurationSnapshot.Limits,
                    upstreamTarget,
                    forwardedHeaders,
                    preferKeepAlive,
                    currentContext,
                    currentContext.RequestId,
                    cancellationToken);

                requestsProcessed = nextRequestCount;
                ApplyForwardingResult(currentContext, result);
                if (requestsProcessed >= _configurationSnapshot.ConnectionLimits.MaxRequestsPerClientConnection)
                {
                    _metrics.ClientConnectionClosedByMaxRequests();
                    currentContext.KeepClientConnectionOpen = false;
                    CompleteContext(ref currentContext);
                    return;
                }

                CompleteContext(ref currentContext);
                if (!result.Succeeded || !result.KeepClientConnectionOpen)
                {
                    return;
                }
            }
        }
        catch (ProxyTimeoutException exception) when (exception.Kind == ProxyTimeoutKind.ClientRequestHead)
        {
            _metrics.ClientRequestHeadTimedOut();
            _logger.LogDebug(exception, "Client timed out before sending a complete request head.");
            if (currentContext is not null)
            {
                await WriteGeneratedResponseAsync(
                    clientStream,
                    408,
                    "Request Timeout",
                    "Request Timeout",
                    currentContext,
                    ProxyFailureKind.ClientRequestHeadTimeout,
                    cancellationToken);
                CompleteContext(ref currentContext);
            }
        }
        catch (ProxyTimeoutException exception) when (exception.Kind == ProxyTimeoutKind.ClientKeepAliveIdle)
        {
            _metrics.ClientConnectionClosedByIdleTimeout();
            _logger.LogDebug(exception, "Client keep-alive idle timeout elapsed.");
        }
        catch (ProxyTimeoutException exception) when (exception.Kind == ProxyTimeoutKind.DownstreamWrite)
        {
            _metrics.DownstreamWriteTimedOut();
            _logger.LogDebug(exception, "Timed out while writing a generated response to the client.");
            if (currentContext is not null)
            {
                currentContext.FailureKind = ProxyFailureKind.ClientDisconnected;
                CompleteContext(ref currentContext);
            }
        }
        catch (IOException exception) when (IsClientDisconnect(exception))
        {
            _metrics.ClientPrematureDisconnect();
            _logger.LogDebug(exception, "Client disconnected during request processing.");
            if (currentContext is not null)
            {
                currentContext.FailureKind = ProxyFailureKind.ClientDisconnected;
                CompleteContext(ref currentContext);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(requestHeadBuffer);
        }
    }

    private async ValueTask<bool> TryHandleGeneratedRouteActionAsync(
        Stream clientStream,
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
            clientStream,
            actionDecision.Response!,
            context,
            cancellationToken);
        return true;
    }

    private async ValueTask<bool> TryRejectKnownLengthRequestBodyAsync(
        Stream clientStream,
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
            clientStream,
            413,
            "Payload Too Large",
            "Payload Too Large",
            context,
            ProxyFailureKind.RequestPayloadTooLarge,
            cancellationToken);
        return true;
    }

    private async ValueTask<bool> TryHandleCacheHitAsync(
        Stream clientStream,
        RuntimeRoute route,
        Http1RequestHead requestHead,
        string upstreamTarget,
        bool keepClientConnectionOpen,
        ProxyRequestContext context,
        RuntimeTimeouts timeouts,
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

        await WriteCachedResponseAsync(
            clientStream,
            requestHead,
            cachedResponse,
            keepClientConnectionOpen,
            context,
            timeouts,
            cancellationToken);
        return true;
    }

    private async ValueTask<ForwardingResult> ForwardWithRetriesAsync(
        Stream clientStream,
        Http1HeadReadResult requestHeadRead,
        Http1RequestHead requestHead,
        RuntimeRoute route,
        RuntimeListener listener,
        RuntimeTimeouts timeouts,
        RuntimeConnectionLimits connectionLimits,
        RuntimeLimits limits,
        string upstreamTarget,
        ForwardedHeadersContext forwardedHeaders,
        bool preferClientKeepAlive,
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
                    clientStream,
                    503,
                    "Service Unavailable",
                    "Service Unavailable",
                    context,
                    ProxyFailureKind.NoHealthyUpstream,
                    cancellationToken);
                return new ForwardingResult(false, true, false, 503, ProxyFailureKind.NoHealthyUpstream);
            }

            context.SetUpstream(ProxyRequestContextRuntimeMapper.ToRequestUpstream(selection.Upstream));
            var suppressGeneratedFailureResponse = retryAllowed && attempt < maxAttempts;
            var result = await _forwarder.ForwardAsync(
                clientStream,
                requestHeadRead,
                requestHead,
                route,
                selection.Upstream,
                listener,
                ProxyTimeoutPolicy.ApplyRetryAttemptTimeout(route, timeouts),
                connectionLimits,
                limits,
                upstreamTarget,
                forwardedHeaders,
                preferClientKeepAlive,
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
                    await Task.Delay(route.Retry.RetryBackoff, _timeProvider, cancellationToken);
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
                return await WriteSuppressedFailureAsync(clientStream, result, context, cancellationToken);
            }

            return result;
        }

        if (lastResult is not null && !lastResult.ResponseStarted)
        {
            return await WriteSuppressedFailureAsync(clientStream, lastResult, context, cancellationToken);
        }

        return lastResult ?? new ForwardingResult(false, false, false, null, ProxyFailureKind.NoHealthyUpstream);
    }

    private async ValueTask<bool> HandleUpgradeAsync(
        Stream clientStream,
        Http1RequestHead requestHead,
        ForwardedHeadersContext forwardedHeaders,
        ProxyRequestContext context,
        CancellationToken cancellationToken)
    {
        context.IsUpgrade = true;
        _metrics.UpgradeRequestReceived();
        if (!_rateLimiter.TryAcquireUpgrade(
            forwardedHeaders.ResolvedClientAddress,
            _configurationSnapshot.Limits.UpgradeRequestsPerMinutePerIp))
        {
            _metrics.UpgradeRequestRejected();
            await WriteGeneratedResponseAsync(
                clientStream,
                429,
                "Too Many Requests",
                "Too Many Requests",
                context,
                ProxyFailureKind.UpgradeRateLimited,
                cancellationToken);
            return false;
        }

        if (!_upgradeRequestPolicy.TryValidate(requestHead, out var upgrade, out var rejectionReason) || upgrade is null)
        {
            _metrics.UpgradeRequestRejected();
            _metrics.MalformedRequestRejected();
            _logger.LogDebug(
                "Rejected Upgrade request for {Method} {Target}: {RejectionReason}",
                requestHead.Method,
                requestHead.Target,
                rejectionReason);
            await WriteGeneratedResponseAsync(
                clientStream,
                400,
                "Bad Request",
                "Bad Request",
                context,
                ProxyFailureKind.UpgradeValidationFailed,
                cancellationToken);
            return false;
        }

        var upgradeRouteMatch = _routeMatcher.Match(_configurationSnapshot, requestHead);
        if (upgradeRouteMatch is null)
        {
            _metrics.UpgradeRequestRejected();
            await WriteGeneratedResponseAsync(
                clientStream,
                404,
                "Not Found",
                "Not Found",
                context,
                ProxyFailureKind.NoMatchingRoute,
                cancellationToken);
            return false;
        }
        context.SetRoute(ProxyRequestContextRuntimeMapper.ToRequestRoute(upgradeRouteMatch.Route));

        var actionDecision = _routeActionPolicy.Evaluate(
            upgradeRouteMatch.Route,
            requestHead,
            _listener,
            isUpgradeRequest: true);
        if (!actionDecision.ShouldProxy)
        {
            await WriteGeneratedRouteResponseAsync(
                clientStream,
                actionDecision.Response!,
                context,
                cancellationToken);
            return false;
        }

        var upgradeSelection = _upstreamSelector.Select(upgradeRouteMatch.Route);
        if (upgradeSelection is null)
        {
            _metrics.UpgradeRequestRejected();
            await WriteGeneratedResponseAsync(
                clientStream,
                503,
                "Service Unavailable",
                "Service Unavailable",
                context,
                ProxyFailureKind.NoHealthyUpstream,
                cancellationToken);
            return false;
        }
        context.SetUpstream(ProxyRequestContextRuntimeMapper.ToRequestUpstream(upgradeSelection.Upstream));

        var upstreamTarget = _pathRewritePolicy.Apply(upgradeRouteMatch.Route, requestHead.Target, requestHead.Path);
        var effectiveTimeouts = ProxyTimeoutPolicy.ApplyRouteTimeouts(upgradeRouteMatch.Route, _configurationSnapshot.Timeouts);
        var upgradeResult = await _upgradeForwarder.ForwardAsync(
            clientStream,
            requestHead,
            upgrade,
            upgradeRouteMatch.Route,
            upgradeSelection.Upstream,
            _listener,
            effectiveTimeouts,
            _configurationSnapshot.ConnectionLimits,
            upstreamTarget,
            forwardedHeaders,
            context.RequestId,
            cancellationToken);
        if (!upgradeResult.Succeeded)
        {
            _healthStore.RecordRequestFailure(upgradeSelection.Upstream);
            _circuitBreakerStore.RecordFailure(
                upgradeSelection.CircuitBreakerLease,
                ProxyForwardingFailurePolicy.CircuitFailureReason(upgradeResult.FailureKind));
        }
        else
        {
            _circuitBreakerStore.RecordSuccess(upgradeSelection.CircuitBreakerLease);
        }

        ApplyForwardingResult(context, upgradeResult);
        return false;
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
                selection.CircuitBreakerLease.Dispose();
            }

            return;
        }

        _circuitBreakerStore.RecordSuccess(selection.CircuitBreakerLease);
    }

    private async ValueTask<ForwardingResult> WriteSuppressedFailureAsync(
        Stream clientStream,
        ForwardingResult result,
        ProxyRequestContext context,
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
            clientStream,
            statusCode,
            reason,
            reason,
            context,
            result.FailureKind,
            cancellationToken);
        return result with
        {
            ResponseStarted = true,
            KeepClientConnectionOpen = false,
            ResponseStatusCode = statusCode
        };
    }

    private async ValueTask<Http1HeadReadResult> ReadRequestHeadAsync(
        Stream clientStream,
        byte[] requestHeadBuffer,
        int maxRequestHeadBytes,
        CancellationToken cancellationToken)
    {
        var totalBytesRead = 0;

        while (totalBytesRead < maxRequestHeadBytes)
        {
            var bytesRead = await clientStream.ReadAsync(
                requestHeadBuffer.AsMemory(totalBytesRead, 1),
                cancellationToken);

            if (bytesRead == 0)
            {
                var code = totalBytesRead == 0 ? EmptyRequestHead : IncompleteRequestHead;
                return new Http1HeadReadResult(code, totalBytesRead, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);
            }

            totalBytesRead += bytesRead;
            _metrics.AddBytesRead(bytesRead);

            var requestHeadLength = FindRequestHeadLength(requestHeadBuffer.AsSpan(0, totalBytesRead));
            if (requestHeadLength > 0)
            {
                return new Http1HeadReadResult(
                    requestHeadLength,
                    totalBytesRead,
                    requestHeadBuffer.AsMemory(0, requestHeadLength),
                    ReadOnlyMemory<byte>.Empty);
            }
        }

        return new Http1HeadReadResult(RequestHeadTooLarge, totalBytesRead, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);
    }

    private async ValueTask WriteGeneratedResponseAsync(
        Stream clientStream,
        int statusCode,
        string reasonPhrase,
        string body,
        ProxyRequestContext context,
        ProxyFailureKind failureKind,
        CancellationToken cancellationToken)
    {
        await ProxyErrorResponses.WriteGeneratedAsync(
            clientStream,
            statusCode,
            reasonPhrase,
            body,
            context.RequestId,
            _configurationSnapshot.Timeouts.DownstreamWriteTimeout,
            _metrics,
            cancellationToken,
            contentType: "text/plain",
            headers: WithAltSvc([]));

        context.ResponseStarted = true;
        context.ResponseStatusCode = statusCode;
        context.FailureKind = failureKind;
        context.KeepClientConnectionOpen = false;
    }

    private async ValueTask WriteCachedResponseAsync(
        Stream clientStream,
        Http1RequestHead requestHead,
        CachedProxyResponse response,
        bool keepClientConnectionOpen,
        ProxyRequestContext context,
        RuntimeTimeouts timeouts,
        CancellationToken cancellationToken)
    {
        var includeBody = !string.Equals(requestHead.Method, "HEAD", StringComparison.OrdinalIgnoreCase);
        var ageSeconds = ProxyCacheAgePolicy.CalculateAgeSeconds(
            response.StoredAtUtc,
            _timeProvider.GetUtcNow());
        var builder = new StringBuilder();
        builder.Append("HTTP/1.1 ")
            .Append(response.StatusCode)
            .Append(' ')
            .Append(response.ReasonPhrase)
            .Append("\r\n");

        foreach (var header in response.Headers)
        {
            builder.Append(header.Name).Append(": ").Append(header.Value).Append("\r\n");
        }

        if (_altSvcPolicy.TryCreateHeader(_listener, out var altSvc))
        {
            builder.Append(altSvc.Name).Append(": ").Append(altSvc.Value).Append("\r\n");
        }

        builder.Append("Age: ").Append(ageSeconds).Append("\r\n");
        builder.Append("X-Request-Id: ").Append(context.RequestId).Append("\r\n");
        builder.Append("Content-Length: ").Append(response.Body.Length).Append("\r\n");
        builder.Append(keepClientConnectionOpen ? "Connection: keep-alive\r\n\r\n" : "Connection: close\r\n\r\n");

        var headBytes = Encoding.ASCII.GetBytes(builder.ToString());
        await ProxyTimeoutPolicy.RunAsync(
            async timeoutToken => await clientStream.WriteAsync(headBytes, timeoutToken),
            timeouts.DownstreamWriteTimeout,
            ProxyTimeoutKind.DownstreamWrite,
            cancellationToken);
        _metrics.AddBytesWritten(headBytes.Length);

        if (includeBody && response.Body.Length > 0)
        {
            await ProxyTimeoutPolicy.RunAsync(
                async timeoutToken => await clientStream.WriteAsync(response.Body, timeoutToken),
                timeouts.DownstreamWriteTimeout,
                ProxyTimeoutKind.DownstreamWrite,
                cancellationToken);
            _metrics.AddBytesWritten(response.Body.Length);
        }

        context.ResponseStarted = true;
        context.ResponseStatusCode = response.StatusCode;
        context.KeepClientConnectionOpen = keepClientConnectionOpen;
        context.SetRouteAction("cache");
    }

    private async ValueTask WriteGeneratedRouteResponseAsync(
        Stream clientStream,
        GeneratedRouteResponse response,
        ProxyRequestContext context,
        CancellationToken cancellationToken)
    {
        await ProxyErrorResponses.WriteGeneratedAsync(
            clientStream,
            response.StatusCode,
            response.ReasonPhrase,
            response.Body,
            context.RequestId,
            _configurationSnapshot.Timeouts.DownstreamWriteTimeout,
            _metrics,
            cancellationToken,
            contentType: response.ContentType,
            headers: WithAltSvc(response.Headers));

        context.ResponseStarted = true;
        context.ResponseStatusCode = response.StatusCode;
        context.KeepClientConnectionOpen = false;
    }

    private IReadOnlyList<ProxyHeaderField> WithAltSvc(IReadOnlyList<ProxyHeaderField> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var result = headers
            .Where(static header => !string.Equals(header.Name, "alt-svc", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (_altSvcPolicy.TryCreateHeader(_listener, out var altSvc))
        {
            result.Add(altSvc);
        }

        return result;
    }

    private ProxyRequestContext CreateRequestContext()
    {
        return new ProxyRequestContext(
            _requestIdGenerator.Create(),
            _listener.Name,
            ProxyRequestContextRuntimeMapper.ToTransport(_listener),
            _socket.RemoteEndPoint?.ToString(),
            _configurationSnapshot.Version,
            _timeProvider);
    }

    private IPEndPoint? GetRemoteEndPoint()
    {
        return _socket.RemoteEndPoint as IPEndPoint;
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

    private static void ApplyForwardingResult(ProxyRequestContext context, ForwardingResult result)
    {
        context.ResponseStarted = result.ResponseStarted;
        context.ResponseStatusCode = result.ResponseStatusCode;
        context.KeepClientConnectionOpen = result.KeepClientConnectionOpen;
        context.FailureKind = result.FailureKind;
        if (result.Tunnel is not null)
        {
            context.TunnelEstablished = result.ResponseStatusCode == 101;
            context.TunnelCloseReason = result.Tunnel.CloseReason;
            context.TunnelBytesClientToUpstream = result.Tunnel.BytesClientToUpstream;
            context.TunnelBytesUpstreamToClient = result.Tunnel.BytesUpstreamToClient;
        }
    }

    private static int FindRequestHeadLength(ReadOnlySpan<byte> bytes)
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

    private static bool IsClientDisconnect(IOException exception)
    {
        return exception.InnerException is SocketException;
    }
}
