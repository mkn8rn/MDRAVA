namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public sealed record ProxyConfigurationFileDiscovery(
    string Path,
    string Format,
    string Status,
    string? Reason);
