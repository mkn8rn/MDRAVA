using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.API.Controllers;

public sealed record ProxyConfigurationReloadResponse<TProjection>(
    bool Succeeded,
    string SourceDirectory,
    DateTimeOffset AttemptedAtUtc,
    int? ActiveVersion,
    DateTimeOffset? LoadedAtUtc,
    DateTimeOffset? LastSuccessfulLoadAtUtc,
    ProxyConfigurationDiscovery Discovery,
    IReadOnlyList<string> Errors,
    IReadOnlyList<ProxyConfigurationFileError> FileErrors,
    TProjection? ActiveConfiguration,
    ProxyListenerReloadResponse? ListenerReload)
    where TProjection : class
{
    public static ProxyConfigurationReloadResponse<TProjection> FromResult(
        ProxyConfigurationReloadResult<TProjection> result)
    {
        return result switch
        {
            ProxyConfigurationReloadResult<TProjection>.LoadFailedResult loadFailed =>
                FromResult(
                    loadFailed,
                    succeeded: false,
                    activeConfiguration: loadFailed.ActiveConfiguration,
                    listenerReload: null),
            ProxyConfigurationReloadResult<TProjection>.ListenerReloadFailedResult listenerReloadFailed =>
                FromResult(
                    listenerReloadFailed,
                    succeeded: false,
                    activeConfiguration: listenerReloadFailed.ActiveConfiguration,
                    listenerReload: ProxyListenerReloadResponse.FromResult(listenerReloadFailed.ListenerReload)),
            ProxyConfigurationReloadResult<TProjection>.ReloadedResult reloaded =>
                FromResult(
                    reloaded,
                    succeeded: true,
                    activeConfiguration: reloaded.ActiveConfiguration,
                    listenerReload: ProxyListenerReloadResponse.FromResult(reloaded.ListenerReload)),
            _ => throw new InvalidOperationException($"Unknown reload result '{result.GetType().Name}'.")
        };
    }

    private static ProxyConfigurationReloadResponse<TProjection> FromResult(
        ProxyConfigurationReloadResult<TProjection> result,
        bool succeeded,
        TProjection? activeConfiguration,
        ProxyListenerReloadResponse? listenerReload)
    {
        return new ProxyConfigurationReloadResponse<TProjection>(
            Succeeded: succeeded,
            SourceDirectory: result.SourceDirectory,
            AttemptedAtUtc: result.AttemptedAtUtc,
            ActiveVersion: result.ActiveVersion,
            LoadedAtUtc: result.LoadedAtUtc,
            LastSuccessfulLoadAtUtc: result.LastSuccessfulLoadAtUtc,
            Discovery: result.Discovery,
            Errors: result.Errors,
            FileErrors: result.FileErrors,
            ActiveConfiguration: activeConfiguration,
            ListenerReload: listenerReload);
    }
}
