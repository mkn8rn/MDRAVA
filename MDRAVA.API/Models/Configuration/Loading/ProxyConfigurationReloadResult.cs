using MDRAVA.API.Proxy.Configuration.Runtime;

namespace MDRAVA.API.Models.Configuration.Loading;

public sealed record ProxyConfigurationReloadResult(
    bool Succeeded,
    string SourceDirectory,
    int? ActiveVersion,
    DateTimeOffset? LoadedAtUtc,
    IReadOnlyList<string> Errors,
    ProxyConfigurationProjection? ActiveConfiguration);
