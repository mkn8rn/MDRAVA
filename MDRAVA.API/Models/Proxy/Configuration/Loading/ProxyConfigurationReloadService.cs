using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Configuration.Storage;

namespace MDRAVA.API.Proxy.Configuration.Loading;

public sealed class ProxyConfigurationReloadService : IProxyConfigurationReloadService
{
    private readonly IProxyConfigurationLoader _loader;
    private readonly IProxyConfigurationStore _store;
    private readonly ILogger<ProxyConfigurationReloadService> _logger;

    public ProxyConfigurationReloadService(
        IProxyConfigurationLoader loader,
        IProxyConfigurationStore store,
        ILogger<ProxyConfigurationReloadService> logger)
    {
        _loader = loader;
        _store = store;
        _logger = logger;
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
                hasExisting && existing is not null ? existing.Version : null,
                existing?.LoadedAtUtc,
                loadResult.Errors,
                existing is null ? null : ProxyConfigurationMapper.ToProjection(existing));
        }

        var snapshot = _store.Replace(loadResult.Snapshot);
        _logger.LogInformation(
            "Proxy configuration version {Version} loaded from {SourcePath}",
            snapshot.Version,
            snapshot.SourceDirectory);

        return new ProxyConfigurationReloadResult(
            true,
            snapshot.SourceDirectory,
            snapshot.Version,
            snapshot.LoadedAtUtc,
            [],
            ProxyConfigurationMapper.ToProjection(snapshot));
    }
}
