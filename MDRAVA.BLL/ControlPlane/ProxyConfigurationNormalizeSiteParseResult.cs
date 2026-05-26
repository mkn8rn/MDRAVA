using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane;

public sealed record ProxyConfigurationNormalizeSiteParseResult(
    SiteOptions? Site,
    string? CanonicalJson,
    string? Error)
{
    public bool Succeeded => Error is null;
}
