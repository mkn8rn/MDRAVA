using BusinessProxyConfigurationProjection =
    MDRAVA.BLL.ControlPlane.ConfigurationManagement.ProxyConfigurationProjection;
using BusinessProxyConfigurationReloadResult =
    MDRAVA.BLL.ControlPlane.ConfigurationManagement.ProxyConfigurationReloadResult<MDRAVA.BLL.ControlPlane.ConfigurationManagement.ProxyConfigurationProjection>;

namespace MDRAVA.API.Controllers;

public sealed record ProxyConfigurationReloadResponse(
    bool Succeeded,
    string SourceDirectory,
    DateTimeOffset AttemptedAtUtc,
    int? ActiveVersion,
    DateTimeOffset? LoadedAtUtc,
    DateTimeOffset? LastSuccessfulLoadAtUtc,
    ProxyConfigurationDiscoveryResponse Discovery,
    IReadOnlyList<string> Errors,
    IReadOnlyList<ProxyConfigurationFileErrorResponse> FileErrors,
    ProxyConfigurationResponse? ActiveConfiguration,
    ProxyListenerReloadResponse? ListenerReload)
{
    public static ProxyConfigurationReloadResponse FromResult(BusinessProxyConfigurationReloadResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result switch
        {
            BusinessProxyConfigurationReloadResult.LoadFailedResult loadFailed =>
                FromResult(
                    loadFailed,
                    succeeded: false,
                    activeConfiguration: loadFailed.ActiveConfiguration,
                    listenerReload: null),
            BusinessProxyConfigurationReloadResult.ListenerReloadFailedResult listenerReloadFailed =>
                FromResult(
                    listenerReloadFailed,
                    succeeded: false,
                    activeConfiguration: listenerReloadFailed.ActiveConfiguration,
                    listenerReload: ProxyListenerReloadResponse.FromResult(listenerReloadFailed.ListenerReload)),
            BusinessProxyConfigurationReloadResult.ReloadedResult reloaded =>
                FromResult(
                    reloaded,
                    succeeded: true,
                    activeConfiguration: reloaded.ActiveConfiguration,
                    listenerReload: ProxyListenerReloadResponse.FromResult(reloaded.ListenerReload)),
            _ => throw new InvalidOperationException($"Unknown reload result '{result.GetType().Name}'.")
        };
    }

    private static ProxyConfigurationReloadResponse FromResult(
        BusinessProxyConfigurationReloadResult result,
        bool succeeded,
        BusinessProxyConfigurationProjection? activeConfiguration,
        ProxyListenerReloadResponse? listenerReload)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new ProxyConfigurationReloadResponse(
            Succeeded: succeeded,
            SourceDirectory: result.SourceDirectory,
            AttemptedAtUtc: result.AttemptedAtUtc,
            ActiveVersion: result.ActiveVersion,
            LoadedAtUtc: result.LoadedAtUtc,
            LastSuccessfulLoadAtUtc: result.LastSuccessfulLoadAtUtc,
            Discovery: ProxyConfigurationDiscoveryResponse.FromDiscovery(result.Discovery),
            Errors: result.Errors.ToArray(),
            FileErrors: ProxyConfigurationFileErrorResponse.FromErrors(result.FileErrors),
            ActiveConfiguration: activeConfiguration is null
                ? null
                : ProxyConfigurationResponse.FromProjection(activeConfiguration),
            ListenerReload: listenerReload);
    }
}
