using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public abstract record ProxyConfigurationNormalizeSiteParseResult
{
    private ProxyConfigurationNormalizeSiteParseResult()
    {
    }

    public static ProxyConfigurationNormalizeSiteParseResult Parsed(
        SiteOptions site,
        string canonicalJson)
    {
        return new ParsedResult(site, canonicalJson);
    }

    public static ProxyConfigurationNormalizeSiteParseResult Failed(string error)
    {
        return new FailedResult(error);
    }

    public sealed record ParsedResult : ProxyConfigurationNormalizeSiteParseResult
    {
        public ParsedResult(SiteOptions site, string canonicalJson)
        {
            ArgumentNullException.ThrowIfNull(site);
            ArgumentException.ThrowIfNullOrWhiteSpace(canonicalJson);

            Site = site;
            CanonicalJson = canonicalJson;
        }

        public SiteOptions Site { get; }

        public string CanonicalJson { get; }
    }

    public sealed record FailedResult : ProxyConfigurationNormalizeSiteParseResult
    {
        public FailedResult(string error)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(error);

            Error = error;
        }

        public string Error { get; }
    }
}
