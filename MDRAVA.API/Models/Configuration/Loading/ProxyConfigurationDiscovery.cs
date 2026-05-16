namespace MDRAVA.API.Models.Configuration.Loading;

public sealed record ProxyConfigurationDiscovery(
    ProxyFilesystemLayout Layout,
    IReadOnlyList<ProxyConfigurationFileDiscovery> Files,
    IReadOnlyList<string> CreatedPaths,
    IReadOnlyList<string> ExistingPaths);
