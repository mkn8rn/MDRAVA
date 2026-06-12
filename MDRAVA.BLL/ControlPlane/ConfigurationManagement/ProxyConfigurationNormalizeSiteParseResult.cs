using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public sealed record ProxyConfigurationNormalizeSiteParseResult
{
    private ProxyConfigurationNormalizeSiteParseResult(
        SiteOptions? site,
        string? canonicalJson,
        string? error)
    {
        Site = site;
        CanonicalJson = canonicalJson;
        Error = error;
    }

    public SiteOptions? Site { get; }

    public string? CanonicalJson { get; }

    public string? Error { get; }

    public bool Succeeded => Error is null;

    public static ProxyConfigurationNormalizeSiteParseResult Parsed(
        SiteOptions? site,
        string? canonicalJson)
    {
        return new ProxyConfigurationNormalizeSiteParseResult(
            site: site,
            canonicalJson: canonicalJson,
            error: null);
    }

    public static ProxyConfigurationNormalizeSiteParseResult Failed(string error)
    {
        return new ProxyConfigurationNormalizeSiteParseResult(
            site: null,
            canonicalJson: null,
            error: error);
    }
}
