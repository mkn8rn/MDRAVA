using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using MDRAVA.API.Proxy.Acme;
using MDRAVA.API.Proxy.Caching;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Configuration.Storage;
using MDRAVA.API.Proxy.Connections;
using MDRAVA.API.Proxy.Forwarding;
using MDRAVA.API.Proxy.Health;
using MDRAVA.API.Proxy.Metrics;
using MDRAVA.API.Proxy.Observability;
using MDRAVA.API.Proxy.Resilience;
using MDRAVA.API.Proxy.Routing;
using MDRAVA.API.Proxy.Runtime;
using MDRAVA.API.Proxy.Tls;

namespace MDRAVA.API.Proxy.Hosting;

public sealed class ProxyListenerService : BackgroundService, IProxyListenerManager
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
    private readonly ResponseCacheStore _cacheStore;
    private readonly CircuitBreakerStore _circuitBreakerStore;
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
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private readonly CancellationTokenSource _serviceStopping = new();
    private readonly Dictionary<string, ManagedListener> _listeners = new(StringComparer.OrdinalIgnoreCase);

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
        ResponseCacheStore cacheStore,
        CircuitBreakerStore circuitBreakerStore,
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
        _cacheStore = cacheStore;
        _circuitBreakerStore = circuitBreakerStore;
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

    public async ValueTask<ProxyListenerReloadResult> ApplyReloadAsync(
        ProxyConfigurationSnapshot snapshot,
        Func<ProxyConfigurationSnapshot, ProxyConfigurationSnapshot> activateSnapshot,
        CancellationToken cancellationToken)
    {
        await _reloadGate.WaitAsync(cancellationToken);
        try
        {
            _metrics.ListenerReloadAttempted();
            var attemptedAt = DateTimeOffset.UtcNow;
            var nextListeners = snapshot.Listeners
                .Where(static listener => listener.Enabled)
                .ToDictionary(static listener => RuntimeListenerIdentity.From(listener).Key, StringComparer.OrdinalIgnoreCase);
            var diff = DiffListeners(nextListeners);
            Dictionary<string, ManagedListener> pending = new(StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (var key in diff.Added.Concat(diff.Changed))
                {
                    var listener = nextListeners[key];
                    var handle = ManagedListener.Bind(listener);
                    pending.Add(key, handle);
                    _logger.LogInformation(
                        "Proxy listener {ListenerName} prepared on {Address}:{Port}",
                        listener.Name,
                        listener.Address,
                        listener.Port);
                }
            }
            catch (Exception exception) when (exception is SocketException or IOException or InvalidOperationException)
            {
                foreach (var handle in pending.Values)
                {
                    await handle.DisposeWithoutDrainAsync();
                }

                _metrics.ListenerStartFailed();
                _metrics.ListenerReloadFailed();
                var result = BuildReloadResult(
                    false,
                    attemptedAt,
                    diff,
                    pending,
                    [SafeError(exception)]);
                UpdateRuntimeState(result);
                _logger.LogWarning(exception, "Proxy listener reload failed while preparing new listeners.");
                return result;
            }

            ProxyConfigurationSnapshot activeSnapshot;
            Dictionary<string, ManagedListener> existingForResult;
            lock (_listeners)
            {
                existingForResult = new Dictionary<string, ManagedListener>(_listeners, StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                activeSnapshot = activateSnapshot(snapshot);
            }
            catch (Exception exception)
            {
                foreach (var handle in pending.Values)
                {
                    await handle.DisposeWithoutDrainAsync();
                }

                _metrics.ListenerReloadFailed();
                var result = BuildReloadResult(
                    false,
                    attemptedAt,
                    diff,
                    pending,
                    [SafeError(exception)]);
                UpdateRuntimeState(result);
                return result;
            }

            List<ManagedListener> oldHandles = [];
            lock (_listeners)
            {
                foreach (var key in diff.Unchanged)
                {
                    _listeners[key].Update(nextListeners[key]);
                }

                foreach (var key in diff.Changed)
                {
                    if (_listeners.TryGetValue(key, out var old))
                    {
                        oldHandles.Add(old);
                    }

                    _listeners[key] = pending[key];
                }

                foreach (var key in diff.Added)
                {
                    _listeners[key] = pending[key];
                }

                foreach (var key in diff.Removed)
                {
                    if (_listeners.Remove(key, out var old))
                    {
                        oldHandles.Add(old);
                    }
                }
            }

            foreach (var key in diff.Added.Concat(diff.Changed))
            {
                pending[key].Activate(this, _serviceStopping.Token);
            }

            foreach (var old in oldHandles)
            {
                await old.StopAcceptingAsync(activeSnapshot.Limits.ShutdownGracePeriod, cancellationToken);
                _metrics.ListenerDrained();
            }

            var success = BuildReloadResult(true, attemptedAt, diff, pending, [], existingForResult);
            _metrics.ListenerReloadSucceeded(diff.Added.Count, diff.Removed.Count, diff.Changed.Count, diff.Unchanged.Count);
            UpdateRuntimeState(success);
            _logger.LogInformation(
                "Proxy listener reload applied: added={Added} removed={Removed} changed={Changed} unchanged={Unchanged}",
                diff.Added.Count,
                diff.Removed.Count,
                diff.Changed.Count,
                diff.Unchanged.Count);
            return success;
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    public IReadOnlyList<ProxyListenerStatus> Snapshot()
    {
        lock (_listeners)
        {
            return _listeners.Values
                .Select(static listener => listener.Snapshot())
                .OrderBy(static listener => listener.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _serviceStopping.Cancel();
        var snapshot = _configurationStore.Snapshot;
        var shutdownToken = _shutdown.BeginShutdown(snapshot.Limits.ShutdownGracePeriod);
        if (_shutdown.StartedAtUtc is not null && _shutdown.DeadlineUtc is not null)
        {
            _runtimeState.MarkShuttingDown(_shutdown.StartedAtUtc.Value, _shutdown.DeadlineUtc.Value);
        }

        ManagedListener[] listeners;
        await _reloadGate.WaitAsync(cancellationToken);
        try
        {
            lock (_listeners)
            {
                listeners = _listeners.Values.ToArray();
                _listeners.Clear();
            }
        }
        finally
        {
            _reloadGate.Release();
        }

        foreach (var listener in listeners)
        {
            await listener.StopAcceptingAsync(snapshot.Limits.ShutdownGracePeriod, shutdownToken);
        }

        UpdateRuntimeState(null);
        await base.StopAsync(cancellationToken);
        _upstreamConnectionPool.Dispose();
    }

    private ListenerDiff DiffListeners(IReadOnlyDictionary<string, RuntimeListener> nextListeners)
    {
        List<string> added = [];
        List<string> removed = [];
        List<string> changed = [];
        List<string> unchanged = [];

        lock (_listeners)
        {
            foreach (var key in nextListeners.Keys)
            {
                if (!_listeners.TryGetValue(key, out var existing))
                {
                    added.Add(key);
                    continue;
                }

                if (CanReuse(existing.Listener, nextListeners[key]))
                {
                    unchanged.Add(key);
                }
                else
                {
                    changed.Add(key);
                }
            }

            foreach (var key in _listeners.Keys)
            {
                if (!nextListeners.ContainsKey(key))
                {
                    removed.Add(key);
                }
            }
        }

        return new ListenerDiff(
            added.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            removed.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            changed.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            unchanged.Order(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private ProxyListenerReloadResult BuildReloadResult(
        bool succeeded,
        DateTimeOffset attemptedAt,
        ListenerDiff diff,
        IReadOnlyDictionary<string, ManagedListener> pending,
        IReadOnlyList<string> errors,
        IReadOnlyDictionary<string, ManagedListener>? existing = null)
    {
        List<ProxyListenerReloadChange> changes = [];
        existing ??= _listeners;
        AddChanges(changes, "added", diff.Added, pending);
        AddChanges(changes, "removed", diff.Removed, existing);
        AddChanges(changes, "changed", diff.Changed, pending.Count == 0 ? _listeners : pending);
        AddChanges(changes, "unchanged", diff.Unchanged, existing);

        return new ProxyListenerReloadResult(
            succeeded,
            attemptedAt,
            diff.Added.Count,
            diff.Removed.Count,
            diff.Changed.Count,
            diff.Unchanged.Count,
            changes.OrderBy(static change => change.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static change => change.Action, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            errors);
    }

    private static void AddChanges(
        List<ProxyListenerReloadChange> changes,
        string action,
        IReadOnlyList<string> keys,
        IReadOnlyDictionary<string, ManagedListener> handles)
    {
        foreach (var key in keys)
        {
            if (!handles.TryGetValue(key, out var handle))
            {
                continue;
            }

            var status = handle.Snapshot();
            changes.Add(new ProxyListenerReloadChange(
                action,
                status.Name,
                status.Identity,
                status.BindKey,
                status.State.ToString(),
                status.LastError));
        }
    }

    private void UpdateRuntimeState(ProxyListenerReloadResult? lastReload)
    {
        var listeners = Snapshot();
        _metrics.SetActiveListeners(listeners.Count(static listener => listener.State == ProxyListenerState.Active));
        _runtimeState.ReplaceListeners(listeners, lastReload);
    }

    private async Task AcceptLoopAsync(ManagedListener handle, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Socket clientSocket;

                try
                {
                    clientSocket = await handle.Socket.AcceptAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                _metrics.ConnectionAccepted();
                var requestSnapshot = _configurationStore.Snapshot;
                var requestListener = ResolveRequestListener(requestSnapshot, handle.Listener);
                var admissionLease = _admission.TryAcquireClientConnection(requestSnapshot.Limits.MaxActiveClientConnections);
                if (admissionLease is null)
                {
                    clientSocket.Dispose();
                    _metrics.ConnectionClosed();
                    continue;
                }

                var connectionTask = RunConnectionAsync(clientSocket, requestSnapshot, requestListener, admissionLease, _shutdown.Token);
                handle.AddConnectionTask(connectionTask);
            }
        }
        catch (Exception exception) when (exception is SocketException or IOException)
        {
            handle.MarkFailed(SafeError(exception));
            _logger.LogWarning(exception, "Proxy listener {ListenerName} stopped after socket failure.", handle.Listener.Name);
        }
        catch (Exception exception)
        {
            handle.MarkFailed(SafeError(exception));
            _logger.LogError(exception, "Proxy listener {ListenerName} stopped unexpectedly.", handle.Listener.Name);
        }
        finally
        {
            UpdateRuntimeState(null);
        }
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
                _cacheStore,
                _circuitBreakerStore,
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

    private static bool CanReuse(RuntimeListener current, RuntimeListener next)
    {
        return string.Equals(current.Address, next.Address, StringComparison.OrdinalIgnoreCase)
            && current.Port == next.Port
            && current.Transport == next.Transport;
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

    private static string SafeError(Exception exception)
    {
        return exception switch
        {
            SocketException socket => socket.SocketErrorCode.ToString(),
            IOException => "io_error",
            InvalidOperationException => "invalid_operation",
            _ => exception.GetType().Name
        };
    }

    private sealed record ListenerDiff(
        IReadOnlyList<string> Added,
        IReadOnlyList<string> Removed,
        IReadOnlyList<string> Changed,
        IReadOnlyList<string> Unchanged);

    private sealed class ManagedListener
    {
        private readonly ConcurrentDictionary<Task, byte> _connectionTasks = new();
        private readonly object _gate = new();
        private RuntimeListener _listener;
        private ProxyListenerState _state = ProxyListenerState.Starting;
        private DateTimeOffset? _startedAtUtc;
        private DateTimeOffset? _stoppedAtUtc;
        private string? _lastError;
        private Task? _acceptTask;

        private ManagedListener(RuntimeListener listener, Socket socket)
        {
            _listener = listener;
            Socket = socket;
        }

        public RuntimeListener Listener
        {
            get
            {
                lock (_gate)
                {
                    return _listener;
                }
            }
        }

        public Socket Socket { get; }

        public static ManagedListener Bind(RuntimeListener listener)
        {
            var listenAddress = IPAddress.Parse(listener.Address);
            var listenEndPoint = new IPEndPoint(listenAddress, listener.Port);
            var socket = new Socket(listenAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
                ExclusiveAddressUse = false
            };

            try
            {
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                socket.Bind(listenEndPoint);
                socket.Listen(listener.Backlog);
                return new ManagedListener(listener, socket);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        public void Activate(ProxyListenerService owner, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                if (_acceptTask is not null)
                {
                    return;
                }

                _state = ProxyListenerState.Active;
                _startedAtUtc = DateTimeOffset.UtcNow;
                _stoppedAtUtc = null;
                _lastError = null;
                _acceptTask = Task.Run(() => owner.AcceptLoopAsync(this, cancellationToken), CancellationToken.None);
            }
        }

        public void Update(RuntimeListener listener)
        {
            lock (_gate)
            {
                _listener = listener;
            }
        }

        public void AddConnectionTask(Task task)
        {
            _connectionTasks.TryAdd(task, 0);
            _ = task.ContinueWith(
                completed => _connectionTasks.TryRemove(completed, out _),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        public async ValueTask StopAcceptingAsync(TimeSpan drainGracePeriod, CancellationToken cancellationToken)
        {
            Task? acceptTask;
            lock (_gate)
            {
                if (_state is not ProxyListenerState.Stopped and not ProxyListenerState.Failed)
                {
                    _state = ProxyListenerState.Draining;
                }

                acceptTask = _acceptTask;
            }

            Socket.Dispose();
            if (acceptTask is not null)
            {
                try
                {
                    await acceptTask.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
            }

            var activeTasks = _connectionTasks.Keys.ToArray();
            if (activeTasks.Length > 0)
            {
                var allConnections = Task.WhenAll(activeTasks);
                var timeout = Task.Delay(drainGracePeriod, cancellationToken);
                await Task.WhenAny(allConnections, timeout);
            }

            lock (_gate)
            {
                _state = ProxyListenerState.Stopped;
                _stoppedAtUtc = DateTimeOffset.UtcNow;
            }
        }

        public ValueTask DisposeWithoutDrainAsync()
        {
            Socket.Dispose();
            lock (_gate)
            {
                _state = ProxyListenerState.Stopped;
                _stoppedAtUtc = DateTimeOffset.UtcNow;
            }

            return ValueTask.CompletedTask;
        }

        public void MarkFailed(string error)
        {
            lock (_gate)
            {
                _state = ProxyListenerState.Failed;
                _stoppedAtUtc = DateTimeOffset.UtcNow;
                _lastError = error;
            }
        }

        public ProxyListenerStatus Snapshot()
        {
            lock (_gate)
            {
                var identity = RuntimeListenerIdentity.From(_listener);
                return new ProxyListenerStatus(
                    _listener.Name,
                    identity.Key,
                    identity.BindKey,
                    _listener.Address,
                    _listener.Port,
                    _listener.Transport.ToString().ToLowerInvariant(),
                    identity.TlsEnabled,
                    _state,
                    _connectionTasks.Count,
                    _startedAtUtc,
                    _stoppedAtUtc,
                    _lastError);
            }
        }
    }
}
