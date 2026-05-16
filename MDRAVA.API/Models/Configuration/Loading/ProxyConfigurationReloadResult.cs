using MDRAVA.API.Proxy.Configuration.Runtime;

namespace MDRAVA.API.Models.Configuration.Loading;

public sealed record ProxyConfigurationReloadResult(
    bool Succeeded,
    string SourceDirectory,
    DateTimeOffset AttemptedAtUtc,
    int? ActiveVersion,
    DateTimeOffset? LoadedAtUtc,
    DateTimeOffset? LastSuccessfulLoadAtUtc,
    IReadOnlyList<string> Errors,
    IReadOnlyList<ProxyConfigurationFileError> FileErrors,
    ProxyConfigurationProjection? ActiveConfiguration);
