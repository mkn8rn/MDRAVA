using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Listeners;

namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public abstract partial record ProxyConfigurationReloadResult<TProjection>
    where TProjection : class
{
    private ProxyConfigurationReloadResult(
        string sourceDirectory,
        DateTimeOffset attemptedAtUtc,
        int? activeVersion,
        DateTimeOffset? loadedAtUtc,
        DateTimeOffset? lastSuccessfulLoadAtUtc,
        ProxyConfigurationDiscovery discovery,
        IReadOnlyList<string> errors,
        IReadOnlyList<ProxyConfigurationFileError> fileErrors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(fileErrors);

        SourceDirectory = sourceDirectory;
        AttemptedAtUtc = attemptedAtUtc;
        ActiveVersion = activeVersion;
        LoadedAtUtc = loadedAtUtc;
        LastSuccessfulLoadAtUtc = lastSuccessfulLoadAtUtc;
        Discovery = discovery;
        Errors = errors.ToArray();
        FileErrors = fileErrors.ToArray();
    }

    public string SourceDirectory { get; }

    public DateTimeOffset AttemptedAtUtc { get; }

    public int? ActiveVersion { get; }

    public DateTimeOffset? LoadedAtUtc { get; }

    public DateTimeOffset? LastSuccessfulLoadAtUtc { get; }

    public ProxyConfigurationDiscovery Discovery { get; }

    public IReadOnlyList<string> Errors { get; }

    public IReadOnlyList<ProxyConfigurationFileError> FileErrors { get; }

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
        return new LoadFailedResult(
            sourceDirectory,
            attemptedAtUtc,
            activeVersion,
            loadedAtUtc,
            discovery,
            errors,
            fileErrors,
            activeConfiguration);
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
        if (listenerReload is not ProxyListenerReloadResult.FailedResult)
        {
            throw new ArgumentException("A listener reload failure result requires a failed listener reload.", nameof(listenerReload));
        }

        return new ListenerReloadFailedResult(
            sourceDirectory,
            attemptedAtUtc,
            activeVersion,
            loadedAtUtc,
            discovery,
            listenerReload,
            activeConfiguration);
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
        if (listenerReload is not ProxyListenerReloadResult.AppliedResult)
        {
            throw new ArgumentException("A successful reload result requires a successful listener reload.", nameof(listenerReload));
        }

        return new ReloadedResult(
            sourceDirectory,
            attemptedAtUtc,
            activeVersion,
            loadedAtUtc,
            discovery,
            listenerReload,
            activeConfiguration);
    }

}
