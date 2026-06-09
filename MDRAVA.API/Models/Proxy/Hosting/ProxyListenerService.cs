#pragma warning disable CA1416
using System.Collections.Concurrent;
using System.Net;
using System.Net.Quic;
using System.Net.Sockets;
using MDRAVA.API.Proxy.Acme;
using MDRAVA.API.Proxy.Caching;
using MDRAVA.API.Proxy.Connections;
using MDRAVA.API.Proxy.Forwarding;
using MDRAVA.API.Proxy.Health;
using MDRAVA.API.Proxy.Http3;
using MDRAVA.API.Proxy.Metrics;
using MDRAVA.API.Proxy.Observability;
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
    private readonly Http3AltSvcPolicy _altSvcPolicy;
    private readonly CircuitBreakerStore _circuitBreakerStore;
    private readonly AcmeHttp01ChallengeResponder _acmeChallengeResponder;
    private readonly TlsConnectionAuthenticator _tlsAuthenticator;
    private readonly IHttp3QuicListenerFactory _quicListenerFactory;
    private readonly ProxyMetrics _metrics;
    private readonly RequestIdGenerator _requestIdGenerator;
    private readonly AccessLogEmitter _accessLogEmitter;
    private readonly ProxyAdmissionController _admission;
    private readonly ProxyShutdownCoordinator _shutdown;
    private readonly UpstreamConnectionPool _upstreamConnectionPool;
    private readonly Http3UpstreamConnectionPool _http3UpstreamConnectionPool;
    private readonly ClientRateLimiter _rateLimiter;
    private readonly ProxyRuntimeState _runtimeState;
    private readonly ProxyListenerReloadPlanner _reloadPlanner;
    private readonly ILogger<ProxyListenerService> _logger;
    private readonly ILogger<ClientConnection> _connectionLogger;
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private readonly CancellationTokenSource _serviceStopping = new();
    private readonly Dictionary<string, ManagedListener> _listeners = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ManagedQuicListener> _quicListeners = new(StringComparer.OrdinalIgnoreCase);

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
        Http3AltSvcPolicy altSvcPolicy,
        CircuitBreakerStore circuitBreakerStore,
        AcmeHttp01ChallengeResponder acmeChallengeResponder,
        TlsConnectionAuthenticator tlsAuthenticator,
        IHttp3QuicListenerFactory quicListenerFactory,
        ProxyMetrics metrics,
        RequestIdGenerator requestIdGenerator,
        AccessLogEmitter accessLogEmitter,
        ProxyAdmissionController admission,
        ProxyShutdownCoordinator shutdown,
        UpstreamConnectionPool upstreamConnectionPool,
        Http3UpstreamConnectionPool http3UpstreamConnectionPool,
        ClientRateLimiter rateLimiter,
        ProxyRuntimeState runtimeState,
        ProxyListenerReloadPlanner reloadPlanner,
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
        _altSvcPolicy = altSvcPolicy;
        _circuitBreakerStore = circuitBreakerStore;
        _acmeChallengeResponder = acmeChallengeResponder;
        _tlsAuthenticator = tlsAuthenticator;
        _quicListenerFactory = quicListenerFactory;
        _metrics = metrics;
        _requestIdGenerator = requestIdGenerator;
        _accessLogEmitter = accessLogEmitter;
        _admission = admission;
        _shutdown = shutdown;
        _upstreamConnectionPool = upstreamConnectionPool;
        _http3UpstreamConnectionPool = http3UpstreamConnectionPool;
        _rateLimiter = rateLimiter;
        _runtimeState = runtimeState;
        _reloadPlanner = reloadPlanner;
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
            var plan = CreateListenerReloadPlan(snapshot);
            var nextListeners = plan.DesiredTcpListeners;
            var nextQuicListeners = plan.DesiredQuicListeners;
            var diff = plan.TcpDiff;
            var quicDiff = plan.QuicDiff;
            Dictionary<string, ManagedListener> pending = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, ManagedQuicListener> pendingQuic = new(StringComparer.OrdinalIgnoreCase);
            List<string> listenerErrors = [];

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
                    quicDiff,
                    pending,
                    pendingQuic,
                    [SafeError(exception)],
                    plan.CurrentTcpListeners,
                    plan.CurrentQuicListeners);
                UpdateRuntimeState(result);
                _logger.LogWarning(exception, "Proxy listener reload failed while preparing new listeners.");
                return result;
            }

            foreach (var key in quicDiff.Added.Concat(quicDiff.Changed))
            {
                var listener = nextQuicListeners[key];
                try
                {
                    var handle = await ManagedQuicListener.BindAsync(
                        listener,
                        snapshot,
                        _quicListenerFactory,
                        cancellationToken);
                    pendingQuic.Add(key, handle);
                    _metrics.QuicListenerStarted();
                    _logger.LogInformation(
                        "HTTP/3 QUIC listener {ListenerName} prepared on {Address}:{Port}",
                        listener.Name,
                        listener.Address,
                        listener.Port);
                }
                catch (Exception exception) when (exception is QuicException or SocketException or IOException or InvalidOperationException or PlatformNotSupportedException)
                {
                    _metrics.QuicListenerStartFailed();
                    _metrics.ListenerStartFailed();
                    var error = $"quic:{listener.Name}:{SafeError(exception)}";
                    listenerErrors.Add(error);
                    pendingQuic.Add(key, ManagedQuicListener.Failed(listener, SafeError(exception)));
                    _logger.LogWarning(exception, "HTTP/3 QUIC listener {ListenerName} failed to start.", listener.Name);
                }
            }

            ProxyConfigurationSnapshot activeSnapshot;
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
                foreach (var handle in pendingQuic.Values)
                {
                    await handle.DisposeWithoutDrainAsync();
                }

                _metrics.ListenerReloadFailed();
                var result = BuildReloadResult(
                    false,
                    attemptedAt,
                    diff,
                    quicDiff,
                    pending,
                    pendingQuic,
                    [SafeError(exception)],
                    plan.CurrentTcpListeners,
                    plan.CurrentQuicListeners);
                UpdateRuntimeState(result);
                return result;
            }

            List<ManagedListener> oldHandles = [];
            List<ManagedQuicListener> oldQuicHandles = [];
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
            lock (_quicListeners)
            {
                foreach (var key in quicDiff.Unchanged)
                {
                    _quicListeners[key].Update(nextQuicListeners[key]);
                }

                foreach (var key in quicDiff.Changed)
                {
                    if (_quicListeners.TryGetValue(key, out var old))
                    {
                        oldQuicHandles.Add(old);
                    }

                    _quicListeners[key] = pendingQuic[key];
                }

                foreach (var key in quicDiff.Added)
                {
                    _quicListeners[key] = pendingQuic[key];
                }

                foreach (var key in quicDiff.Removed)
                {
                    if (_quicListeners.Remove(key, out var old))
                    {
                        oldQuicHandles.Add(old);
                    }
                }
            }

            foreach (var key in diff.Added.Concat(diff.Changed))
            {
                pending[key].Activate(this, _serviceStopping.Token);
            }
            foreach (var key in quicDiff.Added.Concat(quicDiff.Changed))
            {
                pendingQuic[key].Activate(this, _serviceStopping.Token);
            }

            foreach (var old in oldHandles)
            {
                await old.StopAcceptingAsync(activeSnapshot.Limits.ShutdownGracePeriod, cancellationToken);
                _metrics.ListenerDrained();
            }
            foreach (var old in oldQuicHandles)
            {
                await old.StopAcceptingAsync(activeSnapshot.Limits.ShutdownGracePeriod, cancellationToken);
                _metrics.ListenerDrained();
            }

            var success = BuildReloadResult(
                true,
                attemptedAt,
                diff,
                quicDiff,
                pending,
                pendingQuic,
                listenerErrors,
                plan.CurrentTcpListeners,
                plan.CurrentQuicListeners);
            _metrics.ListenerReloadSucceeded(
                diff.Added.Count + quicDiff.Added.Count,
                diff.Removed.Count + quicDiff.Removed.Count,
                diff.Changed.Count + quicDiff.Changed.Count,
                diff.Unchanged.Count + quicDiff.Unchanged.Count);
            UpdateRuntimeState(success);
            _logger.LogInformation(
                "Proxy listener reload applied: added={Added} removed={Removed} changed={Changed} unchanged={Unchanged}",
                diff.Added.Count + quicDiff.Added.Count,
                diff.Removed.Count + quicDiff.Removed.Count,
                diff.Changed.Count + quicDiff.Changed.Count,
                diff.Unchanged.Count + quicDiff.Unchanged.Count);
            return success;
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    public IReadOnlyList<ProxyListenerStatus> Snapshot()
    {
        List<ProxyListenerStatus> statuses = [];
        lock (_listeners)
        {
            statuses.AddRange(_listeners.Values.Select(static listener => listener.Snapshot()));
        }

        lock (_quicListeners)
        {
            statuses.AddRange(_quicListeners.Values.Select(static listener => listener.Snapshot()));
        }

        return statuses
            .OrderBy(static listener => listener.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static listener => listener.Kind, StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
        ManagedQuicListener[] quicListeners;
        await _reloadGate.WaitAsync(cancellationToken);
        try
        {
            lock (_listeners)
            {
                listeners = _listeners.Values.ToArray();
                _listeners.Clear();
            }
            lock (_quicListeners)
            {
                quicListeners = _quicListeners.Values.ToArray();
                _quicListeners.Clear();
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
        foreach (var listener in quicListeners)
        {
            await listener.StopAcceptingAsync(snapshot.Limits.ShutdownGracePeriod, shutdownToken);
        }

        UpdateRuntimeState(null);
        await base.StopAsync(cancellationToken);
        _upstreamConnectionPool.Dispose();
        _http3UpstreamConnectionPool.Dispose();
    }

    private ListenerReloadPlan CreateListenerReloadPlan(ProxyConfigurationSnapshot snapshot)
    {
        var desiredTcpListeners = snapshot.Listeners
            .Where(static listener => listener.Enabled && listener.TcpTrafficEnabled)
            .ToDictionary(static listener => RuntimeListenerIdentity.From(listener).Key, StringComparer.OrdinalIgnoreCase);
        var desiredQuicListeners = snapshot.Listeners
            .Where(static listener => listener.Enabled && listener.Http3.EnabledForTraffic)
            .ToDictionary(static listener => listener.QuicIdentity!.Key, StringComparer.OrdinalIgnoreCase);
        var currentTcpListeners = SnapshotTcpListeners();
        var currentQuicListeners = SnapshotQuicListeners();
        var reloadPlan = _reloadPlanner.CreatePlan(
            ToTcpReloadTargets(currentTcpListeners),
            ToTcpReloadTargets(desiredTcpListeners),
            ToQuicReloadTargets(currentQuicListeners),
            ToQuicReloadTargets(desiredQuicListeners));

        return new ListenerReloadPlan(
            desiredTcpListeners,
            desiredQuicListeners,
            currentTcpListeners,
            currentQuicListeners,
            reloadPlan.TcpDiff,
            reloadPlan.QuicDiff);
    }

    private Dictionary<string, ManagedListener> SnapshotTcpListeners()
    {
        lock (_listeners)
        {
            return new Dictionary<string, ManagedListener>(_listeners, StringComparer.OrdinalIgnoreCase);
        }
    }

    private Dictionary<string, ManagedQuicListener> SnapshotQuicListeners()
    {
        lock (_quicListeners)
        {
            return new Dictionary<string, ManagedQuicListener>(_quicListeners, StringComparer.OrdinalIgnoreCase);
        }
    }

    private static Dictionary<string, ProxyTcpListenerReloadTarget> ToTcpReloadTargets(
        IReadOnlyDictionary<string, RuntimeListener> desiredListeners)
    {
        Dictionary<string, ProxyTcpListenerReloadTarget> targets = new(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, listener) in desiredListeners)
        {
            targets[key] = ToTcpReloadTarget(key, listener);
        }

        return targets;
    }

    private static Dictionary<string, ProxyTcpListenerReloadTarget> ToTcpReloadTargets(
        IReadOnlyDictionary<string, ManagedListener> currentListeners)
    {
        Dictionary<string, ProxyTcpListenerReloadTarget> targets = new(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, handle) in currentListeners)
        {
            targets[key] = ToTcpReloadTarget(key, handle.Listener);
        }

        return targets;
    }

    private static ProxyTcpListenerReloadTarget ToTcpReloadTarget(string key, RuntimeListener listener)
    {
        return new ProxyTcpListenerReloadTarget(
            key,
            listener.Address,
            listener.Port,
            listener.Transport.ToString());
    }

    private static Dictionary<string, ProxyQuicListenerReloadTarget> ToQuicReloadTargets(
        IReadOnlyDictionary<string, RuntimeListener> desiredListeners)
    {
        Dictionary<string, ProxyQuicListenerReloadTarget> targets = new(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, listener) in desiredListeners)
        {
            targets[key] = ToQuicReloadTarget(key, listener, failed: false);
        }

        return targets;
    }

    private static Dictionary<string, ProxyQuicListenerReloadTarget> ToQuicReloadTargets(
        IReadOnlyDictionary<string, ManagedQuicListener> currentListeners)
    {
        Dictionary<string, ProxyQuicListenerReloadTarget> targets = new(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, handle) in currentListeners)
        {
            targets[key] = ToQuicReloadTarget(key, handle.Listener, handle.State == ProxyListenerState.Failed);
        }

        return targets;
    }

    private static ProxyQuicListenerReloadTarget ToQuicReloadTarget(string key, RuntimeListener listener, bool failed)
    {
        return new ProxyQuicListenerReloadTarget(
            key,
            listener.Address,
            listener.Port,
            listener.Transport.ToString(),
            listener.Http3Enablement.ToConfigText(),
            failed);
    }

    private ProxyListenerReloadResult BuildReloadResult(
        bool succeeded,
        DateTimeOffset attemptedAt,
        ProxyListenerDiff diff,
        ProxyListenerDiff quicDiff,
        IReadOnlyDictionary<string, ManagedListener> pending,
        IReadOnlyDictionary<string, ManagedQuicListener> pendingQuic,
        IReadOnlyList<string> errors,
        IReadOnlyDictionary<string, ManagedListener>? existing = null,
        IReadOnlyDictionary<string, ManagedQuicListener>? existingQuic = null)
    {
        List<ProxyListenerReloadChange> changes = [];
        existing ??= _listeners;
        existingQuic ??= _quicListeners;
        AddChanges(changes, "added", diff.Added, pending);
        AddChanges(changes, "removed", diff.Removed, existing);
        AddChanges(changes, "changed", diff.Changed, pending.Count == 0 ? _listeners : pending);
        AddChanges(changes, "unchanged", diff.Unchanged, existing);
        AddQuicChanges(changes, "added", quicDiff.Added, pendingQuic);
        AddQuicChanges(changes, "removed", quicDiff.Removed, existingQuic);
        AddQuicChanges(changes, "changed", quicDiff.Changed, pendingQuic.Count == 0 ? _quicListeners : pendingQuic);
        AddQuicChanges(changes, "unchanged", quicDiff.Unchanged, existingQuic);

        return new ProxyListenerReloadResult(
            succeeded,
            attemptedAt,
            diff.Added.Count + quicDiff.Added.Count,
            diff.Removed.Count + quicDiff.Removed.Count,
            diff.Changed.Count + quicDiff.Changed.Count,
            diff.Unchanged.Count + quicDiff.Unchanged.Count,
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

    private static void AddQuicChanges(
        List<ProxyListenerReloadChange> changes,
        string action,
        IReadOnlyList<string> keys,
        IReadOnlyDictionary<string, ManagedQuicListener> handles)
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
        _metrics.SetActiveQuicListeners(listeners.Count(static listener => listener.Kind == "quic" && listener.State == ProxyListenerState.Active));
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

    private async Task AcceptQuicLoopAsync(ManagedQuicListener handle, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && handle.ListenerHandle is not null)
            {
                QuicConnection connection;
                try
                {
                    connection = await handle.ListenerHandle.AcceptConnectionAsync(cancellationToken);
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
                    _metrics.ConnectionClosed();
                    await connection.CloseAsync(0x100, CancellationToken.None);
                    await connection.DisposeAsync();
                    continue;
                }

                var connectionTask = RunQuicConnectionAsync(connection, requestSnapshot, requestListener, admissionLease, _shutdown.Token);
                handle.AddConnectionTask(connectionTask);
            }
        }
        catch (Exception exception) when (exception is QuicException or SocketException or IOException)
        {
            handle.MarkFailed(SafeError(exception));
            _logger.LogWarning(exception, "HTTP/3 QUIC listener {ListenerName} stopped after transport failure.", handle.Listener.Name);
        }
        catch (Exception exception)
        {
            handle.MarkFailed(SafeError(exception));
            _logger.LogError(exception, "HTTP/3 QUIC listener {ListenerName} stopped unexpectedly.", handle.Listener.Name);
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
                _altSvcPolicy,
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

    private async Task RunQuicConnectionAsync(
        QuicConnection connection,
        ProxyConfigurationSnapshot snapshot,
        RuntimeListener listener,
        AdmissionLease admissionLease,
        CancellationToken cancellationToken)
    {
        using var ownedAdmission = admissionLease;
        try
        {
            var http3Connection = new Http3Connection(
                connection,
                snapshot,
                listener,
                _routeMatcher,
                _upstreamSelector,
                _healthStore,
                _forwarder,
                _forwardedHeadersPolicy,
                _routeActionPolicy,
                _pathRewritePolicy,
                _cacheStore,
                _circuitBreakerStore,
                _acmeChallengeResponder,
                _metrics,
                _requestIdGenerator,
                _accessLogEmitter,
                _rateLimiter,
                _connectionLogger);

            await http3Connection.RunAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is QuicException or IOException)
        {
            _logger.LogDebug(exception, "HTTP/3 client connection ended with an I/O error.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "HTTP/3 client connection failed unexpectedly.");
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

    private static string SafeError(Exception exception)
    {
        return exception switch
        {
            SocketException socket => socket.SocketErrorCode.ToString(),
            QuicException => "quic_error",
            IOException => "io_error",
            PlatformNotSupportedException => "platform_not_supported",
            InvalidOperationException => "invalid_operation",
            _ => exception.GetType().Name
        };
    }

    private sealed record ListenerReloadPlan(
        IReadOnlyDictionary<string, RuntimeListener> DesiredTcpListeners,
        IReadOnlyDictionary<string, RuntimeListener> DesiredQuicListeners,
        IReadOnlyDictionary<string, ManagedListener> CurrentTcpListeners,
        IReadOnlyDictionary<string, ManagedQuicListener> CurrentQuicListeners,
        ProxyListenerDiff TcpDiff,
        ProxyListenerDiff QuicDiff);

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
                    "tcp",
                    _listener.Address,
                    _listener.Port,
                    _listener.Transport.ToString().ToLowerInvariant(),
                    identity.TlsEnabled,
                    ListenerProtocolAdvertisement.ToConfigText(_listener.Protocols),
                    _listener.Http3.ToStatus(),
                    _listener.Http2Limits.MaxConcurrentStreams,
                    _listener.Http2Limits.MaxHeaderListBytes,
                    _listener.Http2Limits.MaxFrameSize,
                    _state,
                    _connectionTasks.Count,
                    _startedAtUtc,
                    _stoppedAtUtc,
                    _lastError);
            }
        }
    }

    private sealed class ManagedQuicListener
    {
        private readonly ConcurrentDictionary<Task, byte> _connectionTasks = new();
        private readonly object _gate = new();
        private RuntimeListener _listener;
        private ProxyListenerState _state = ProxyListenerState.Starting;
        private DateTimeOffset? _startedAtUtc;
        private DateTimeOffset? _stoppedAtUtc;
        private string? _lastError;
        private Task? _acceptTask;

        private ManagedQuicListener(RuntimeListener listener, QuicListener? listenerHandle)
        {
            _listener = listener;
            ListenerHandle = listenerHandle;
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

        public ProxyListenerState State
        {
            get
            {
                lock (_gate)
                {
                    return _state;
                }
            }
        }

        public QuicListener? ListenerHandle { get; }

        public static async ValueTask<ManagedQuicListener> BindAsync(
            RuntimeListener listener,
            ProxyConfigurationSnapshot snapshot,
            IHttp3QuicListenerFactory factory,
            CancellationToken cancellationToken)
        {
            var listenerHandle = await factory.ListenAsync(listener, snapshot, cancellationToken);
            return new ManagedQuicListener(listener, listenerHandle);
        }

        public static ManagedQuicListener Failed(RuntimeListener listener, string error)
        {
            return new ManagedQuicListener(listener, null)
            {
                _state = ProxyListenerState.Failed,
                _stoppedAtUtc = DateTimeOffset.UtcNow,
                _lastError = error
            };
        }

        public void Activate(ProxyListenerService owner, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                if (_acceptTask is not null || ListenerHandle is null || _state == ProxyListenerState.Failed)
                {
                    return;
                }

                _state = ProxyListenerState.Active;
                _startedAtUtc = DateTimeOffset.UtcNow;
                _stoppedAtUtc = null;
                _lastError = null;
                _acceptTask = Task.Run(() => owner.AcceptQuicLoopAsync(this, cancellationToken), CancellationToken.None);
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

            if (ListenerHandle is not null)
            {
                await ListenerHandle.DisposeAsync();
            }

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

        public async ValueTask DisposeWithoutDrainAsync()
        {
            if (ListenerHandle is not null)
            {
                await ListenerHandle.DisposeAsync();
            }

            lock (_gate)
            {
                _state = ProxyListenerState.Stopped;
                _stoppedAtUtc = DateTimeOffset.UtcNow;
            }
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
                var identity = RuntimeQuicListenerIdentity.From(_listener);
                return new ProxyListenerStatus(
                    _listener.Name,
                    identity.Key,
                    identity.BindKey,
                    "quic",
                    _listener.Address,
                    _listener.Port,
                    "udp/quic",
                    identity.TlsEnabled,
                    ListenerProtocolAdvertisement.ToConfigText(_listener.Protocols),
                    _listener.Http3.ToStatus(),
                    _listener.Http2Limits.MaxConcurrentStreams,
                    _listener.Http2Limits.MaxHeaderListBytes,
                    _listener.Http2Limits.MaxFrameSize,
                    _state,
                    _connectionTasks.Count,
                    _startedAtUtc,
                    _stoppedAtUtc,
                    _lastError);
            }
        }
    }
}
