using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Listeners;

namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public sealed record ProxyConfigurationReloadResult<TProjection>
    where TProjection : class
{
    private ProxyConfigurationReloadResult(
        bool succeeded,
        string sourceDirectory,
        DateTimeOffset attemptedAtUtc,
        int? activeVersion,
        DateTimeOffset? loadedAtUtc,
        DateTimeOffset? lastSuccessfulLoadAtUtc,
        ProxyConfigurationDiscovery discovery,
        IReadOnlyList<string> errors,
        IReadOnlyList<ProxyConfigurationFileError> fileErrors,
        TProjection? activeConfiguration,
        ProxyListenerReloadResult? listenerReload)
    {
        Succeeded = succeeded;
        SourceDirectory = sourceDirectory;
        AttemptedAtUtc = attemptedAtUtc;
        ActiveVersion = activeVersion;
        LoadedAtUtc = loadedAtUtc;
        LastSuccessfulLoadAtUtc = lastSuccessfulLoadAtUtc;
        Discovery = discovery;
        Errors = errors;
        FileErrors = fileErrors;
        ActiveConfiguration = activeConfiguration;
        ListenerReload = listenerReload;
    }

    public bool Succeeded { get; }

    public string SourceDirectory { get; }

    public DateTimeOffset AttemptedAtUtc { get; }

    public int? ActiveVersion { get; }

    public DateTimeOffset? LoadedAtUtc { get; }

    public DateTimeOffset? LastSuccessfulLoadAtUtc { get; }

    public ProxyConfigurationDiscovery Discovery { get; }

    public IReadOnlyList<string> Errors { get; }

    public IReadOnlyList<ProxyConfigurationFileError> FileErrors { get; }

    public TProjection? ActiveConfiguration { get; }

    public ProxyListenerReloadResult? ListenerReload { get; }

    public static ProxyConfigurationReloadResult<TProjection> LoadFailed(
        string sourceDirectory,
        DateTimeOffset attemptedAtUtc,
        int? activeVersion,
        DateTimeOffset? loadedAtUtc,
        ProxyConfigurationDiscovery discovery,
        IReadOnlyList<string> errors,
        IReadOnlyList<ProxyConfigurationFileError> fileErrors,
        TProjection? activeConfiguration)
    {
        return new ProxyConfigurationReloadResult<TProjection>(
            succeeded: false,
            sourceDirectory: sourceDirectory,
            attemptedAtUtc: attemptedAtUtc,
            activeVersion: activeVersion,
            loadedAtUtc: loadedAtUtc,
            lastSuccessfulLoadAtUtc: loadedAtUtc,
            discovery: discovery,
            errors: errors,
            fileErrors: fileErrors,
            activeConfiguration: activeConfiguration,
            listenerReload: null);
    }

    public static ProxyConfigurationReloadResult<TProjection> ListenerReloadFailed(
        string sourceDirectory,
        DateTimeOffset attemptedAtUtc,
        int? activeVersion,
        DateTimeOffset? loadedAtUtc,
        ProxyConfigurationDiscovery discovery,
        ProxyListenerReloadResult listenerReload,
        TProjection? activeConfiguration)
    {
        if (listenerReload.Succeeded)
        {
            throw new ArgumentException("A listener reload failure result requires a failed listener reload.", nameof(listenerReload));
        }

        return new ProxyConfigurationReloadResult<TProjection>(
            succeeded: false,
            sourceDirectory: sourceDirectory,
            attemptedAtUtc: attemptedAtUtc,
            activeVersion: activeVersion,
            loadedAtUtc: loadedAtUtc,
            lastSuccessfulLoadAtUtc: loadedAtUtc,
            discovery: discovery,
            errors: listenerReload.Errors,
            fileErrors: listenerReload.Errors.Select(static error => ProxyConfigurationFileError.Global(error)).ToArray(),
            activeConfiguration: activeConfiguration,
            listenerReload: listenerReload);
    }

    public static ProxyConfigurationReloadResult<TProjection> Reloaded(
        string sourceDirectory,
        DateTimeOffset attemptedAtUtc,
        int activeVersion,
        DateTimeOffset loadedAtUtc,
        ProxyConfigurationDiscovery discovery,
        ProxyListenerReloadResult listenerReload,
        TProjection activeConfiguration)
    {
        if (!listenerReload.Succeeded)
        {
            throw new ArgumentException("A successful reload result requires a successful listener reload.", nameof(listenerReload));
        }

        return new ProxyConfigurationReloadResult<TProjection>(
            succeeded: true,
            sourceDirectory: sourceDirectory,
            attemptedAtUtc: attemptedAtUtc,
            activeVersion: activeVersion,
            loadedAtUtc: loadedAtUtc,
            lastSuccessfulLoadAtUtc: loadedAtUtc,
            discovery: discovery,
            errors: [],
            fileErrors: [],
            activeConfiguration: activeConfiguration,
            listenerReload: listenerReload);
    }
}
