using BusinessProxyConfigurationProjection =
    MDRAVA.BLL.ControlPlane.ConfigurationManagement.ProxyConfigurationProjection;
using BusinessProxyConfigurationReloadResult =
    MDRAVA.BLL.ControlPlane.ConfigurationManagement.ProxyConfigurationReloadResult<MDRAVA.BLL.ControlPlane.ConfigurationManagement.ProxyConfigurationProjection>;

namespace MDRAVA.API.Controllers;

public sealed record ProxyConfigurationReloadResponse
{
    public ProxyConfigurationReloadResponse(
        bool succeeded,
        string sourceDirectory,
        DateTimeOffset attemptedAtUtc,
        int? activeVersion,
        DateTimeOffset? loadedAtUtc,
        DateTimeOffset? lastSuccessfulLoadAtUtc,
        ProxyConfigurationDiscoveryResponse discovery,
        IReadOnlyList<string> errors,
        IReadOnlyList<ProxyConfigurationFileErrorResponse> fileErrors,
        ProxyConfigurationResponse? activeConfiguration,
        ProxyListenerReloadResponse? listenerReload)
    {
        ArgumentNullException.ThrowIfNull(discovery);

        Succeeded = succeeded;
        SourceDirectory = sourceDirectory;
        AttemptedAtUtc = attemptedAtUtc;
        ActiveVersion = activeVersion;
        LoadedAtUtc = loadedAtUtc;
        LastSuccessfulLoadAtUtc = lastSuccessfulLoadAtUtc;
        Discovery = discovery;
        Errors = ApiResponseList.Copy(errors);
        FileErrors = ApiResponseList.Copy(fileErrors);
        ActiveConfiguration = activeConfiguration;
        ListenerReload = listenerReload;
    }

    public bool Succeeded { get; }

    public string SourceDirectory { get; }

    public DateTimeOffset AttemptedAtUtc { get; }

    public int? ActiveVersion { get; }

    public DateTimeOffset? LoadedAtUtc { get; }

    public DateTimeOffset? LastSuccessfulLoadAtUtc { get; }

    public ProxyConfigurationDiscoveryResponse Discovery { get; }

    public IReadOnlyList<string> Errors { get; }

    public IReadOnlyList<ProxyConfigurationFileErrorResponse> FileErrors { get; }

    public ProxyConfigurationResponse? ActiveConfiguration { get; }

    public ProxyListenerReloadResponse? ListenerReload { get; }

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
            succeeded: succeeded,
            sourceDirectory: result.SourceDirectory,
            attemptedAtUtc: result.AttemptedAtUtc,
            activeVersion: result.ActiveVersion,
            loadedAtUtc: result.LoadedAtUtc,
            lastSuccessfulLoadAtUtc: result.LastSuccessfulLoadAtUtc,
            discovery: ProxyConfigurationDiscoveryResponse.FromDiscovery(result.Discovery),
            errors: result.Errors,
            fileErrors: ProxyConfigurationFileErrorResponse.FromErrors(result.FileErrors),
            activeConfiguration: activeConfiguration is null
                ? null
                : ProxyConfigurationResponse.FromProjection(activeConfiguration),
            listenerReload: listenerReload);
    }
}
