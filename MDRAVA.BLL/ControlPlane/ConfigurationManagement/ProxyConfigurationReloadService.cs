using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Caching;
using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.ControlPlane.Listeners;

namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public sealed class ProxyConfigurationReloadService
    : IProxyConfigurationReloadOperations<ProxyConfigurationProjection>,
        IProxyConfigurationValidationOperations
{
    private readonly IProxyConfigurationLoader _loader;
    private readonly IProxyConfigurationStore _store;
    private readonly ResponseCacheStore _cacheStore;
    private readonly IProxyConfigurationReloadMetricsSink _metrics;
    private readonly IProxyListenerReloadApplier _listenerReloadApplier;
    private readonly IProxyConfigurationReloadEventSink _events;
    private readonly IRuntimeHttp3PlatformSupportSource _http3PlatformSupportSource;

    public ProxyConfigurationReloadService(
        IProxyConfigurationLoader loader,
        IProxyConfigurationStore store,
        ResponseCacheStore cacheStore,
        IProxyConfigurationReloadMetricsSink metrics,
        IProxyListenerReloadApplier listenerReloadApplier,
        IProxyConfigurationReloadEventSink events,
        IRuntimeHttp3PlatformSupportSource http3PlatformSupportSource)
    {
        _loader = loader;
        _store = store;
        _cacheStore = cacheStore;
        _metrics = metrics;
        _listenerReloadApplier = listenerReloadApplier;
        _events = events;
        _http3PlatformSupportSource = http3PlatformSupportSource;
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
            return ProxyConfigurationReloadResult<ProxyConfigurationProjection>.LoadFailed(
                sourceDirectory: loadResult.SourceDirectory,
                attemptedAtUtc: loadResult.AttemptedAtUtc,
                activeVersion: hasExisting && existing is not null ? existing.Version : null,
                loadedAtUtc: existing?.LoadedAtUtc,
                discovery: loadResult.Discovery,
                errors: loadResult.Errors,
                fileErrors: loadResult.FileErrors,
                activeConfiguration: existing is null ? null : ToProjection(existing));
        }

        var listenerReload = await _listenerReloadApplier.ApplyReloadAsync(
            loadResult.Snapshot,
            candidate => _store.Replace(candidate),
            cancellationToken);
        if (!listenerReload.Succeeded)
        {
            _metrics.ConfigReloadFailed();
            var hasExisting = _store.TryGetSnapshot(out var existing);
            return ProxyConfigurationReloadResult<ProxyConfigurationProjection>.ListenerReloadFailed(
                sourceDirectory: loadResult.SourceDirectory,
                attemptedAtUtc: loadResult.AttemptedAtUtc,
                activeVersion: hasExisting && existing is not null ? existing.Version : null,
                loadedAtUtc: existing?.LoadedAtUtc,
                discovery: loadResult.Discovery,
                listenerReload: listenerReload,
                activeConfiguration: existing is null ? null : ToProjection(existing));
        }

        var snapshot = _store.Snapshot;
        _metrics.ConfigReloadSucceeded();
        _cacheStore.Clear("reload");
        _events.Loaded(snapshot.Version, snapshot.SourceDirectory);

        return ProxyConfigurationReloadResult<ProxyConfigurationProjection>.Reloaded(
            sourceDirectory: snapshot.SourceDirectory,
            attemptedAtUtc: loadResult.AttemptedAtUtc,
            activeVersion: snapshot.Version,
            loadedAtUtc: snapshot.LoadedAtUtc,
            discovery: loadResult.Discovery,
            listenerReload: listenerReload,
            activeConfiguration: ToProjection(snapshot));
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

    private ProxyConfigurationProjection ToProjection(ProxyConfigurationSnapshot snapshot)
    {
        return ProxyConfigurationProjectionMapper.ToProjection(
            snapshot,
            _http3PlatformSupportSource.Read());
    }
}
