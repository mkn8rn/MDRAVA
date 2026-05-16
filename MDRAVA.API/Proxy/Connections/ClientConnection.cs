using System.Buffers;
using System.Net.Sockets;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Forwarding;
using MDRAVA.API.Proxy.Health;
using MDRAVA.API.Proxy.Metrics;
using MDRAVA.API.Proxy.Observability;
using MDRAVA.API.Proxy.Protocol;
using MDRAVA.API.Proxy.Routing;
using MDRAVA.API.Proxy.Tls;

namespace MDRAVA.API.Proxy.Connections;

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
    private readonly TlsConnectionAuthenticator _tlsAuthenticator;
    private readonly ProxyMetrics _metrics;
    private readonly RequestIdGenerator _requestIdGenerator;
    private readonly AccessLogEmitter _accessLogEmitter;
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
        TlsConnectionAuthenticator tlsAuthenticator,
        ProxyMetrics metrics,
        RequestIdGenerator requestIdGenerator,
        AccessLogEmitter accessLogEmitter,
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
        _tlsAuthenticator = tlsAuthenticator;
        _metrics = metrics;
        _requestIdGenerator = requestIdGenerator;
        _accessLogEmitter = accessLogEmitter;
        _logger = logger;
    }

    public async ValueTask RunAsync(CancellationToken cancellationToken)
    {
        _socket.NoDelay = true;

        var transportStream = new NetworkStream(_socket, ownsSocket: true);
        Stream clientStream = transportStream;
        if (_listener.Transport == RuntimeListenerTransport.Https)
        {
            var tlsStream = await _tlsAuthenticator.AuthenticateAsync(
                transportStream,
                _configurationSnapshot,
                _listener,
                cancellationToken);
            if (tlsStream is null)
            {
                return;
            }

            clientStream = tlsStream;
        }

        await using var ownedClientStream = clientStream;
        var requestHeadBuffer = ArrayPool<byte>.Shared.Rent(_listener.MaxRequestHeadBytes);
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
                    timeoutToken => ReadRequestHeadAsync(clientStream, requestHeadBuffer, timeoutToken),
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
                    _metrics.MalformedRequestRejected();
                    await WriteGeneratedResponseAsync(
                        clientStream,
                        431,
                        "Request Header Fields Too Large",
                        "Request Head Too Large",
                        currentContext,
                        ProxyFailureKind.ClientMalformedRequest,
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
                if (!Http1RequestParser.TryParse(requestHeadBytes.Span, out var requestHead, out var parseError))
                {
                    _metrics.ParseFailed();
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
                    ExtractExternalRequestId(requestHead));

                if (IsUnsupportedConnectionMethod(requestHead.Method))
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

                if (_upgradeRequestPolicy.IsUpgradeRequest(requestHead))
                {
                    var shouldContinue = await HandleUpgradeAsync(
                        clientStream,
                        requestHeadRead,
                        requestHead,
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
                currentContext.SetRoute(routeMatch.Route);

                var selection = _upstreamSelector.Select(routeMatch.Route);
                if (selection is null)
                {
                    await WriteGeneratedResponseAsync(
                        clientStream,
                        503,
                        "Service Unavailable",
                        "Service Unavailable",
                        currentContext,
                        ProxyFailureKind.NoHealthyUpstream,
                        cancellationToken);
                    CompleteContext(ref currentContext);
                    return;
                }
                currentContext.SetUpstream(selection.Upstream);

                var nextRequestCount = requestsProcessed + 1;
                var preferKeepAlive = ShouldKeepClientConnectionOpen(requestHead)
                    && nextRequestCount < _configurationSnapshot.ConnectionLimits.MaxRequestsPerClientConnection;
                var result = await _forwarder.ForwardAsync(
                    clientStream,
                    requestHeadRead,
                    requestHead,
                    selection.Upstream,
                    _listener,
                    _configurationSnapshot.Timeouts,
                    _configurationSnapshot.ConnectionLimits,
                    preferKeepAlive,
                    currentContext.RequestId,
                    cancellationToken);

                if (!result.Succeeded)
                {
                    _healthStore.RecordRequestFailure(selection.Upstream);
                }

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

    private async ValueTask<bool> HandleUpgradeAsync(
        Stream clientStream,
        Http1HeadReadResult requestHeadRead,
        Http1RequestHead requestHead,
        ProxyRequestContext context,
        CancellationToken cancellationToken)
    {
        _ = requestHeadRead;
        context.IsUpgrade = true;
        _metrics.UpgradeRequestReceived();
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
        context.SetRoute(upgradeRouteMatch.Route);

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
        context.SetUpstream(upgradeSelection.Upstream);

        var upgradeResult = await _upgradeForwarder.ForwardAsync(
            clientStream,
            requestHead,
            upgrade,
            upgradeSelection.Upstream,
            _listener,
            _configurationSnapshot.Timeouts,
            _configurationSnapshot.ConnectionLimits,
            context.RequestId,
            cancellationToken);
        if (!upgradeResult.Succeeded)
        {
            _healthStore.RecordRequestFailure(upgradeSelection.Upstream);
        }

        ApplyForwardingResult(context, upgradeResult);
        return false;
    }

    private async ValueTask<Http1HeadReadResult> ReadRequestHeadAsync(
        Stream clientStream,
        byte[] requestHeadBuffer,
        CancellationToken cancellationToken)
    {
        var totalBytesRead = 0;

        while (totalBytesRead < _listener.MaxRequestHeadBytes)
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
            cancellationToken);

        context.ResponseStarted = true;
        context.ResponseStatusCode = statusCode;
        context.FailureKind = failureKind;
        context.KeepClientConnectionOpen = false;
    }

    private ProxyRequestContext CreateRequestContext()
    {
        return new ProxyRequestContext(
            _requestIdGenerator.Create(),
            _listener.Name,
            _listener.Transport,
            _socket.RemoteEndPoint?.ToString(),
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
            _configurationSnapshot.Observability.AccessLogEnabled,
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

    private static string? ExtractExternalRequestId(Http1RequestHead requestHead)
    {
        foreach (var header in requestHead.Headers)
        {
            if (!string.Equals(header.Name, "X-Request-Id", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = header.Value.Trim();
            return IsValidExternalRequestId(value) ? value : null;
        }

        return null;
    }

    private static bool IsValidExternalRequestId(string value)
    {
        if (value.Length is 0 or > 128)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsUnsupportedConnectionMethod(string method)
    {
        return string.Equals(method, "CONNECT", StringComparison.Ordinal);
    }

    private static bool ShouldKeepClientConnectionOpen(Http1RequestHead requestHead)
    {
        if (ProxyForwarder.HasConnectionToken(requestHead.Headers, "close"))
        {
            return false;
        }

        return string.Equals(requestHead.Version, "HTTP/1.1", StringComparison.OrdinalIgnoreCase);
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
