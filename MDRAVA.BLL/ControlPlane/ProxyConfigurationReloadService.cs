using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Caching;
using MDRAVA.BLL.Infrastructure;

namespace MDRAVA.BLL.ControlPlane;

public sealed class ProxyConfigurationReloadService
    : IProxyConfigurationReloadOperations<ProxyConfigurationProjection>,
        IProxyConfigurationValidationOperations
{
    private readonly IProxyConfigurationLoader _loader;
    private readonly IProxyConfigurationStore _store;
    private readonly ResponseCacheStore _cacheStore;
    private readonly ProxyMetrics _metrics;
    private readonly IProxyListenerReloadApplier _listenerReloadApplier;
    private readonly IProxyConfigurationReloadEventSink _events;

    public ProxyConfigurationReloadService(
        IProxyConfigurationLoader loader,
        IProxyConfigurationStore store,
        ResponseCacheStore cacheStore,
        ProxyMetrics metrics,
        IProxyListenerReloadApplier listenerReloadApplier,
        IProxyConfigurationReloadEventSink events)
    {
        _loader = loader;
        _store = store;
        _cacheStore = cacheStore;
        _metrics = metrics;
        _listenerReloadApplier = listenerReloadApplier;
        _events = events;
    }

    public async ValueTask<ProxyConfigurationReloadResult<ProxyConfigurationProjection>> ReloadAsync(
        CancellationToken cancellationToken)
    {
        var loadResult = await _loader.LoadAsync(cancellationToken);
        if (!loadResult.Succeeded || loadResult.Snapshot is null)
        {
            _metrics.ConfigReloadFailed();
            _events.LoadFailed(loadResult.SourceDirectory, loadResult.Errors);

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

        var listenerReload = await _listenerReloadApplier.ApplyReloadAsync(
            loadResult.Snapshot,
            candidate => _store.Replace(candidate),
            cancellationToken);
        if (!listenerReload.Succeeded)
        {
            _metrics.ConfigReloadFailed();
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

        var snapshot = _store.Snapshot;
        _metrics.ConfigReloadSucceeded();
        _cacheStore.Clear("reload");
        _events.Loaded(snapshot.Version, snapshot.SourceDirectory);

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
