namespace MDRAVA.API.Models.Configuration.Loading;

public sealed record ProxyConfigurationFileDiscovery(
    string Path,
    string Format,
    string Status,
    string? Reason);
