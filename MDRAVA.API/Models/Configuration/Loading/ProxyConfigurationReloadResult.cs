using MDRAVA.API.Proxy.Configuration.Runtime;

namespace MDRAVA.API.Models.Configuration.Loading;

public sealed record ProxyConfigurationReloadResult(
    bool Succeeded,
    string SourceDirectory,
    DateTimeOffset AttemptedAtUtc,
    int? ActiveVersion,
    DateTimeOffset? LoadedAtUtc,
    DateTimeOffset? LastSuccessfulLoadAtUtc,
    ProxyConfigurationDiscovery Discovery,
    IReadOnlyList<string> Errors,
    IReadOnlyList<ProxyConfigurationFileError> FileErrors,
    ProxyConfigurationProjection? ActiveConfiguration)
{
    public ProxyListenerReloadResult? ListenerReload { get; init; }
}
