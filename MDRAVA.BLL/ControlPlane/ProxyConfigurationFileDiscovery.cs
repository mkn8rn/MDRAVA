namespace MDRAVA.BLL.ControlPlane;

public sealed record ProxyConfigurationFileDiscovery(
    string Path,
    string Format,
    string Status,
    string? Reason);
