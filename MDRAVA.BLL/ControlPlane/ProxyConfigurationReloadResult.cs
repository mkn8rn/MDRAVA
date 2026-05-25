namespace MDRAVA.BLL.ControlPlane;

public sealed record ProxyConfigurationReloadResult<TProjection>(
    bool Succeeded,
    string SourceDirectory,
    DateTimeOffset AttemptedAtUtc,
    int? ActiveVersion,
    DateTimeOffset? LoadedAtUtc,
    DateTimeOffset? LastSuccessfulLoadAtUtc,
    ProxyConfigurationDiscovery Discovery,
    IReadOnlyList<string> Errors,
    IReadOnlyList<ProxyConfigurationFileError> FileErrors,
    TProjection? ActiveConfiguration)
    where TProjection : class
{
    public ProxyListenerReloadResult? ListenerReload { get; init; }
}
