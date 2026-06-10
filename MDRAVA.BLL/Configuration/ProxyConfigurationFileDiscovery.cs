namespace MDRAVA.BLL.Configuration;

public sealed record ProxyConfigurationFileDiscovery(
    string Path,
    string Format,
    string Status,
    string? Reason);
