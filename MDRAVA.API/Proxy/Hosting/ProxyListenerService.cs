using System.Net;
using System.Net.Sockets;
using MDRAVA.API.Proxy.Connections;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Configuration.Storage;
using MDRAVA.API.Proxy.Forwarding;
using MDRAVA.API.Proxy.Health;
using MDRAVA.API.Proxy.Metrics;
using MDRAVA.API.Proxy.Routing;
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
    private readonly TlsConnectionAuthenticator _tlsAuthenticator;
    private readonly ProxyMetrics _metrics;
    private readonly ProxyRuntimeState _runtimeState;
    private readonly ILogger<ProxyListenerService> _logger;
    private readonly ILogger<ClientConnection> _connectionLogger;
    private Socket? _listenSocket;

    public ProxyListenerService(
        IProxyConfigurationStore configurationStore,
        IRouteMatcher routeMatcher,
        IUpstreamSelector upstreamSelector,
        UpstreamHealthStore healthStore,
        ProxyForwarder forwarder,
        UpgradeForwarder upgradeForwarder,
        UpgradeRequestPolicy upgradeRequestPolicy,
        TlsConnectionAuthenticator tlsAuthenticator,
        ProxyMetrics metrics,
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
        _tlsAuthenticator = tlsAuthenticator;
        _metrics = metrics;
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
                _ = RunConnectionAsync(clientSocket, requestSnapshot, requestListener, stoppingToken);
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
        var stopTask = base.StopAsync(cancellationToken);
        _listenSocket?.Dispose();
        await stopTask;
    }

    private async Task RunConnectionAsync(
        Socket clientSocket,
        ProxyConfigurationSnapshot snapshot,
        RuntimeListener listener,
        CancellationToken cancellationToken)
    {
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
                _tlsAuthenticator,
                _metrics,
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
