using MDRAVA.BLL.ControlPlane;
namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public sealed record ProxyConfigurationDiscovery(
    ProxyFilesystemLayout Layout,
    IReadOnlyList<ProxyConfigurationFileDiscovery> Files,
    IReadOnlyList<string> CreatedPaths,
    IReadOnlyList<string> ExistingPaths);
