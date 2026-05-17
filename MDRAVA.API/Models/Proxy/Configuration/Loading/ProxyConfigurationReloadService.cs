using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Configuration.Storage;
using MDRAVA.API.Proxy.Caching;

namespace MDRAVA.API.Proxy.Configuration.Loading;

public sealed class ProxyConfigurationReloadService : IProxyConfigurationReloadService
{
    private readonly IProxyConfigurationLoader _loader;
    private readonly IProxyConfigurationStore _store;
    private readonly ResponseCacheStore? _cacheStore;
    private readonly ILogger<ProxyConfigurationReloadService> _logger;

    public ProxyConfigurationReloadService(
        IProxyConfigurationLoader loader,
        IProxyConfigurationStore store,
        ResponseCacheStore? cacheStore,
        ILogger<ProxyConfigurationReloadService> logger)
    {
        _loader = loader;
        _store = store;
        _cacheStore = cacheStore;
        _logger = logger;
    }

    public ProxyConfigurationReloadService(
        IProxyConfigurationLoader loader,
        IProxyConfigurationStore store,
        ILogger<ProxyConfigurationReloadService> logger)
        : this(loader, store, null, logger)
    {
    }

    public async ValueTask<ProxyConfigurationReloadResult> ReloadAsync(CancellationToken cancellationToken)
    {
        var loadResult = await _loader.LoadAsync(cancellationToken);
        if (!loadResult.Succeeded || loadResult.Snapshot is null)
        {
            _logger.LogWarning(
                "Proxy configuration reload failed from {SourcePath}: {Errors}",
                loadResult.SourceDirectory,
                string.Join("; ", loadResult.Errors));

            var hasExisting = _store.TryGetSnapshot(out var existing);
            return new ProxyConfigurationReloadResult(
                false,
                loadResult.SourceDirectory,
                loadResult.AttemptedAtUtc,
                hasExisting && existing is not null ? existing.Version : null,
                existing?.LoadedAtUtc,
                existing?.LoadedAtUtc,
                loadResult.Discovery,
                loadResult.Errors,
                loadResult.FileErrors,
                existing is null ? null : ProxyConfigurationMapper.ToProjection(existing));
        }

        var snapshot = _store.Replace(loadResult.Snapshot);
        _cacheStore?.Clear("reload");
        _logger.LogInformation(
            "Proxy configuration version {Version} loaded from {SourcePath}",
            snapshot.Version,
            snapshot.SourceDirectory);

        return new ProxyConfigurationReloadResult(
            true,
            snapshot.SourceDirectory,
            loadResult.AttemptedAtUtc,
            snapshot.Version,
            snapshot.LoadedAtUtc,
            snapshot.LoadedAtUtc,
            loadResult.Discovery,
            [],
            [],
            ProxyConfigurationMapper.ToProjection(snapshot));
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
