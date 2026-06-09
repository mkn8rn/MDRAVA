namespace MDRAVA.API.Proxy.Configuration.Loading;

public sealed class ProxyConfigurationReloadService
    : IProxyConfigurationReloadOperations<ProxyConfigurationProjection>,
        IProxyConfigurationValidationOperations
{
    private readonly IProxyConfigurationLoader _loader;
    private readonly IProxyConfigurationStore _store;
    private readonly ResponseCacheStore? _cacheStore;
    private readonly ProxyMetrics? _metrics;
    private readonly IProxyListenerReloadApplier? _listenerReloadApplier;
    private readonly ILogger<ProxyConfigurationReloadService> _logger;

    public ProxyConfigurationReloadService(
        IProxyConfigurationLoader loader,
        IProxyConfigurationStore store,
        ResponseCacheStore? cacheStore,
        ProxyMetrics? metrics,
        IProxyListenerReloadApplier? listenerReloadApplier,
        ILogger<ProxyConfigurationReloadService> logger)
    {
        _loader = loader;
        _store = store;
        _cacheStore = cacheStore;
        _metrics = metrics;
        _listenerReloadApplier = listenerReloadApplier;
        _logger = logger;
    }

    public ProxyConfigurationReloadService(
        IProxyConfigurationLoader loader,
        IProxyConfigurationStore store,
        ResponseCacheStore? cacheStore,
        ProxyMetrics? metrics,
        ILogger<ProxyConfigurationReloadService> logger)
        : this(loader, store, cacheStore, metrics, null, logger)
    {
    }

    public ProxyConfigurationReloadService(
        IProxyConfigurationLoader loader,
        IProxyConfigurationStore store,
        ResponseCacheStore? cacheStore,
        ILogger<ProxyConfigurationReloadService> logger)
        : this(loader, store, cacheStore, null, null, logger)
    {
    }

    public ProxyConfigurationReloadService(
        IProxyConfigurationLoader loader,
        IProxyConfigurationStore store,
        ILogger<ProxyConfigurationReloadService> logger)
        : this(loader, store, null, logger)
    {
    }

    public async ValueTask<ProxyConfigurationReloadResult<ProxyConfigurationProjection>> ReloadAsync(
        CancellationToken cancellationToken)
    {
        var loadResult = await _loader.LoadAsync(cancellationToken);
        if (!loadResult.Succeeded || loadResult.Snapshot is null)
        {
            _metrics?.ConfigReloadFailed();
            _logger.LogWarning(
                "Proxy configuration reload failed from {SourcePath}: {Errors}",
                loadResult.SourceDirectory,
                string.Join("; ", loadResult.Errors));

            var hasExisting = _store.TryGetSnapshot(out var existing);
            return new ProxyConfigurationReloadResult<ProxyConfigurationProjection>(
                false,
                loadResult.SourceDirectory,
                loadResult.AttemptedAtUtc,
                hasExisting && existing is not null ? existing.Version : null,
                existing?.LoadedAtUtc,
                existing?.LoadedAtUtc,
                loadResult.Discovery,
                loadResult.Errors,
                loadResult.FileErrors,
                existing is null ? null : ProxyConfigurationProjectionMapper.ToProjection(existing));
        }

        ProxyListenerReloadResult? listenerReload = null;
        ProxyConfigurationSnapshot snapshot;
        if (_listenerReloadApplier is null)
        {
            snapshot = _store.Replace(loadResult.Snapshot);
        }
        else
        {
            listenerReload = await _listenerReloadApplier.ApplyReloadAsync(
                loadResult.Snapshot,
                candidate => _store.Replace(candidate),
                cancellationToken);
            if (!listenerReload.Succeeded)
            {
                _metrics?.ConfigReloadFailed();
                var hasExisting = _store.TryGetSnapshot(out var existing);
                return new ProxyConfigurationReloadResult<ProxyConfigurationProjection>(
                    false,
                    loadResult.SourceDirectory,
                    loadResult.AttemptedAtUtc,
                    hasExisting && existing is not null ? existing.Version : null,
                    existing?.LoadedAtUtc,
                    existing?.LoadedAtUtc,
                    loadResult.Discovery,
                    listenerReload.Errors,
                    listenerReload.Errors.Select(static error => new ProxyConfigurationFileError(null, error)).ToArray(),
                    existing is null ? null : ProxyConfigurationProjectionMapper.ToProjection(existing))
                {
                    ListenerReload = listenerReload
                };
            }

            snapshot = _store.Snapshot;
        }

        _metrics?.ConfigReloadSucceeded();
        _cacheStore?.Clear("reload");
        _logger.LogInformation(
            "Proxy configuration version {Version} loaded from {SourcePath}",
            snapshot.Version,
            snapshot.SourceDirectory);

        return new ProxyConfigurationReloadResult<ProxyConfigurationProjection>(
            true,
            snapshot.SourceDirectory,
            loadResult.AttemptedAtUtc,
            snapshot.Version,
            snapshot.LoadedAtUtc,
            snapshot.LoadedAtUtc,
            loadResult.Discovery,
            [],
            [],
            ProxyConfigurationProjectionMapper.ToProjection(snapshot))
        {
            ListenerReload = listenerReload
        };
    }

    public async ValueTask<ProxyConfigurationValidationResult> ValidateAsync(CancellationToken cancellationToken)
    {
        var loadResult = await _loader.ValidateAsync(cancellationToken);
        var hasExisting = _store.TryGetSnapshot(out var existing);
        return new ProxyConfigurationValidationResult(
            loadResult.Succeeded,
            loadResult.SourceDirectory,
            loadResult.AttemptedAtUtc,
            hasExisting && existing is not null ? existing.Version : null,
            existing?.LoadedAtUtc,
            loadResult.WouldBeVersion,
            loadResult.SourceFiles,
            loadResult.Discovery,
            loadResult.Errors,
            loadResult.FileErrors);
    }
}
