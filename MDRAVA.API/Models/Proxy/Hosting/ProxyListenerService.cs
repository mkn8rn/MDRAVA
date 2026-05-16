using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using MDRAVA.API.Proxy.Connections;
using MDRAVA.API.Proxy.Acme;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Configuration.Storage;
using MDRAVA.API.Proxy.Forwarding;
using MDRAVA.API.Proxy.Health;
using MDRAVA.API.Proxy.Metrics;
using MDRAVA.API.Proxy.Observability;
using MDRAVA.API.Proxy.Routing;
using MDRAVA.API.Proxy.Runtime;
using MDRAVA.API.Proxy.Tls;

namespace MDRAVA.API.Proxy.Hosting;

public sealed class ProxyListenerService : BackgroundService
{
    private readonly IProxyConfigurationStore _configurationStore;
    private readonly IRouteMatcher _routeMatcher;
    private readonly IUpstreamSelector _upstreamSelector;
    private readonly UpstreamHealthStore _healthStore;
    private readonly ProxyForwarder _forwarder;
    private readonly UpgradeForwarder _upgradeForwarder;
    private readonly UpgradeRequestPolicy _upgradeRequestPolicy;
    private readonly ForwardedHeadersPolicy _forwardedHeadersPolicy;
    private readonly ProxyRouteActionPolicy _routeActionPolicy;
    private readonly PathRewritePolicy _pathRewritePolicy;
    private readonly AcmeHttp01ChallengeResponder _acmeChallengeResponder;
    private readonly TlsConnectionAuthenticator _tlsAuthenticator;
    private readonly ProxyMetrics _metrics;
    private readonly RequestIdGenerator _requestIdGenerator;
    private readonly AccessLogEmitter _accessLogEmitter;
    private readonly ProxyAdmissionController _admission;
    private readonly ProxyShutdownCoordinator _shutdown;
    private readonly UpstreamConnectionPool _upstreamConnectionPool;
    private readonly ClientRateLimiter _rateLimiter;
    private readonly ProxyRuntimeState _runtimeState;
    private readonly ILogger<ProxyListenerService> _logger;
    private readonly ILogger<ClientConnection> _connectionLogger;
    private readonly ConcurrentDictionary<Task, byte> _connectionTasks = new();
    private Socket? _listenSocket;

    public ProxyListenerService(
        IProxyConfigurationStore configurationStore,
        IRouteMatcher routeMatcher,
        IUpstreamSelector upstreamSelector,
        UpstreamHealthStore healthStore,
        ProxyForwarder forwarder,
        UpgradeForwarder upgradeForwarder,
        UpgradeRequestPolicy upgradeRequestPolicy,
        ForwardedHeadersPolicy forwardedHeadersPolicy,
        ProxyRouteActionPolicy routeActionPolicy,
        PathRewritePolicy pathRewritePolicy,
        AcmeHttp01ChallengeResponder acmeChallengeResponder,
        TlsConnectionAuthenticator tlsAuthenticator,
        ProxyMetrics metrics,
        RequestIdGenerator requestIdGenerator,
        AccessLogEmitter accessLogEmitter,
        ProxyAdmissionController admission,
        ProxyShutdownCoordinator shutdown,
        UpstreamConnectionPool upstreamConnectionPool,
        ClientRateLimiter rateLimiter,
        ProxyRuntimeState runtimeState,
        ILogger<ProxyListenerService> logger,
        ILogger<ClientConnection> connectionLogger)
    {
        _configurationStore = configurationStore;
        _routeMatcher = routeMatcher;
        _upstreamSelector = upstreamSelector;
        _healthStore = healthStore;
        _forwarder = forwarder;
        _upgradeForwarder = upgradeForwarder;
        _upgradeRequestPolicy = upgradeRequestPolicy;
        _forwardedHeadersPolicy = forwardedHeadersPolicy;
        _routeActionPolicy = routeActionPolicy;
        _pathRewritePolicy = pathRewritePolicy;
        _acmeChallengeResponder = acmeChallengeResponder;
        _tlsAuthenticator = tlsAuthenticator;
        _metrics = metrics;
        _requestIdGenerator = requestIdGenerator;
        _accessLogEmitter = accessLogEmitter;
        _admission = admission;
        _shutdown = shutdown;
        _upstreamConnectionPool = upstreamConnectionPool;
        _rateLimiter = rateLimiter;
        _runtimeState = runtimeState;
        _logger = logger;
        _connectionLogger = connectionLogger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var startupSnapshot = _configurationStore.Snapshot;
        if (!startupSnapshot.TryGetFirstEnabledListener(out var listener) || listener is null)
        {
            _runtimeState.MarkStopped("No configured proxy listener.");
            _logger.LogWarning(
                "Proxy dataplane has no configured listener; control-plane endpoints remain available and listener changes still require process restart.");
            return;
        }

        var listenAddress = IPAddress.Parse(listener.Address);
        var listenEndPoint = new IPEndPoint(listenAddress, listener.Port);
        string? stopError = null;

        try
        {
            _listenSocket = new Socket(listenAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
                ExclusiveAddressUse = false
            };

            _listenSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listenSocket.Bind(listenEndPoint);
            _listenSocket.Listen(listener.Backlog);

            _runtimeState.MarkRunning(listener.Name, listenEndPoint);
            _logger.LogInformation(
                "Proxy listener {ListenerName} is accepting HTTP/1.1 connections on {Address}:{Port}",
                listener.Name,
                listener.Address,
                listener.Port);

            while (!stoppingToken.IsCancellationRequested)
            {
                Socket clientSocket;

                try
                {
                    clientSocket = await _listenSocket.AcceptAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                _metrics.ConnectionAccepted();
                var requestSnapshot = _configurationStore.Snapshot;
                var requestListener = ResolveRequestListener(requestSnapshot, listener);
                var admissionLease = _admission.TryAcquireClientConnection(requestSnapshot.Limits.MaxActiveClientConnections);
                if (admissionLease is null)
                {
                    clientSocket.Dispose();
                    continue;
                }

                var connectionTask = RunConnectionAsync(clientSocket, requestSnapshot, requestListener, admissionLease, _shutdown.Token);
                _connectionTasks.TryAdd(connectionTask, 0);
                _ = connectionTask.ContinueWith(
                    completed => _connectionTasks.TryRemove(completed, out _),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            stopError = exception.Message;
            _logger.LogCritical(exception, "Proxy listener failed.");
            throw;
        }
        finally
        {
            _listenSocket?.Dispose();
            _runtimeState.MarkStopped(stopError);
            _logger.LogInformation("Proxy listener stopped.");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        var snapshot = _configurationStore.Snapshot;
        var shutdownToken = _shutdown.BeginShutdown(snapshot.Limits.ShutdownGracePeriod);
        if (_shutdown.StartedAtUtc is not null && _shutdown.DeadlineUtc is not null)
        {
            _runtimeState.MarkShuttingDown(_shutdown.StartedAtUtc.Value, _shutdown.DeadlineUtc.Value);
        }

        _listenSocket?.Dispose();
        await base.StopAsync(cancellationToken);

        var activeTasks = _connectionTasks.Keys.ToArray();
        if (activeTasks.Length > 0)
        {
            try
            {
                await Task.WhenAll(activeTasks).WaitAsync(shutdownToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Proxy shutdown grace period elapsed with {ActiveConnectionCount} active client connection tasks.", _connectionTasks.Count);
            }
        }

        _upstreamConnectionPool.Dispose();
    }

    private async Task RunConnectionAsync(
        Socket clientSocket,
        ProxyConfigurationSnapshot snapshot,
        RuntimeListener listener,
        AdmissionLease admissionLease,
        CancellationToken cancellationToken)
    {
        using var ownedAdmission = admissionLease;
        try
        {
            var connection = new ClientConnection(
                clientSocket,
                snapshot,
                listener,
                _routeMatcher,
                _upstreamSelector,
                _healthStore,
                _forwarder,
                _upgradeForwarder,
                _upgradeRequestPolicy,
                _forwardedHeadersPolicy,
                _routeActionPolicy,
                _pathRewritePolicy,
                _acmeChallengeResponder,
                _tlsAuthenticator,
                _metrics,
                _requestIdGenerator,
                _accessLogEmitter,
                _rateLimiter,
                _connectionLogger);

            await connection.RunAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is SocketException or IOException)
        {
            _logger.LogDebug(exception, "Client connection ended with an I/O error.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Client connection failed unexpectedly.");
        }
        finally
        {
            _metrics.ConnectionClosed();
        }
    }

    private static RuntimeListener ResolveRequestListener(
        ProxyConfigurationSnapshot requestSnapshot,
        RuntimeListener boundListener)
    {
        var current = requestSnapshot.Listeners.FirstOrDefault(listener =>
            string.Equals(listener.Name, boundListener.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(listener.Address, boundListener.Address, StringComparison.OrdinalIgnoreCase)
            && listener.Port == boundListener.Port
            && listener.Transport == boundListener.Transport);

        return current ?? boundListener;
    }
}
