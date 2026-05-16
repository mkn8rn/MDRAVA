using MDRAVA.API.Proxy.Configuration.Runtime;

namespace MDRAVA.API.Proxy.Configuration.Loading;

public sealed record ProxyConfigurationReloadResult(
    bool Succeeded,
    string SourceDirectory,
    int? ActiveVersion,
    DateTimeOffset? LoadedAtUtc,
    IReadOnlyList<string> Errors,
    ProxyConfigurationProjection? ActiveConfiguration);
