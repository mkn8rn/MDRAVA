using ConfigurationManagement = MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.API.Controllers;

public sealed record ProxyConfigurationReloadResponse<TProjection>(
    bool Succeeded,
    string SourceDirectory,
    DateTimeOffset AttemptedAtUtc,
    int? ActiveVersion,
    DateTimeOffset? LoadedAtUtc,
    DateTimeOffset? LastSuccessfulLoadAtUtc,
    ProxyConfigurationDiscoveryResponse Discovery,
    IReadOnlyList<string> Errors,
    IReadOnlyList<ProxyConfigurationFileErrorResponse> FileErrors,
    TProjection? ActiveConfiguration,
    ProxyListenerReloadResponse? ListenerReload)
    where TProjection : class
{
    public static ProxyConfigurationReloadResponse<TProjection> FromResult(
        ConfigurationManagement.ProxyConfigurationReloadResult<TProjection> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result switch
        {
            ConfigurationManagement.ProxyConfigurationReloadResult<TProjection>.LoadFailedResult loadFailed =>
                FromResult(
                    loadFailed,
                    succeeded: false,
                    activeConfiguration: loadFailed.ActiveConfiguration,
                    listenerReload: null),
            ConfigurationManagement.ProxyConfigurationReloadResult<TProjection>.ListenerReloadFailedResult listenerReloadFailed =>
                FromResult(
                    listenerReloadFailed,
                    succeeded: false,
                    activeConfiguration: listenerReloadFailed.ActiveConfiguration,
                    listenerReload: ProxyListenerReloadResponse.FromResult(listenerReloadFailed.ListenerReload)),
            ConfigurationManagement.ProxyConfigurationReloadResult<TProjection>.ReloadedResult reloaded =>
                FromResult(
                    reloaded,
                    succeeded: true,
                    activeConfiguration: reloaded.ActiveConfiguration,
                    listenerReload: ProxyListenerReloadResponse.FromResult(reloaded.ListenerReload)),
            _ => throw new InvalidOperationException($"Unknown reload result '{result.GetType().Name}'.")
        };
    }

    private static ProxyConfigurationReloadResponse<TProjection> FromResult(
        ConfigurationManagement.ProxyConfigurationReloadResult<TProjection> result,
        bool succeeded,
        TProjection? activeConfiguration,
        ProxyListenerReloadResponse? listenerReload)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new ProxyConfigurationReloadResponse<TProjection>(
            Succeeded: succeeded,
            SourceDirectory: result.SourceDirectory,
            AttemptedAtUtc: result.AttemptedAtUtc,
            ActiveVersion: result.ActiveVersion,
            LoadedAtUtc: result.LoadedAtUtc,
            LastSuccessfulLoadAtUtc: result.LastSuccessfulLoadAtUtc,
            Discovery: ProxyConfigurationDiscoveryResponse.FromDiscovery(result.Discovery),
            Errors: result.Errors.ToArray(),
            FileErrors: ProxyConfigurationFileErrorResponse.FromErrors(result.FileErrors),
            ActiveConfiguration: activeConfiguration,
            ListenerReload: listenerReload);
    }
}
