namespace MDRAVA.BLL.Configuration;

public sealed record ProxyConfigurationDiscovery(
    ProxyFilesystemLayout Layout,
    IReadOnlyList<ProxyConfigurationFileDiscovery> Files,
    IReadOnlyList<string> CreatedPaths,
    IReadOnlyList<string> ExistingPaths);
