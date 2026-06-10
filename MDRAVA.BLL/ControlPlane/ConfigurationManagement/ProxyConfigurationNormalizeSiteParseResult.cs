using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public sealed record ProxyConfigurationNormalizeSiteParseResult(
    SiteOptions? Site,
    string? CanonicalJson,
    string? Error)
{
    public bool Succeeded => Error is null;
}
