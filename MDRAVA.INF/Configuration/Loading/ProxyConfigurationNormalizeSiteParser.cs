using MDRAVA.BLL.ControlPlane.ConfigurationManagement;
using System.Text.Json;
using YamlDotNet.Core;

namespace MDRAVA.INF.Configuration.Loading;

public sealed class ProxyConfigurationNormalizeSiteParser
    : IProxyConfigurationNormalizeSiteParser
{
    private readonly SiteConfigurationParser _siteParser;

    public ProxyConfigurationNormalizeSiteParser(SiteConfigurationParser siteParser)
    {
        _siteParser = siteParser;
    }

    public ProxyConfigurationNormalizeSiteParseResult Parse(
        string text,
        ProxyConfigurationNormalizeFormat format)
    {
        var siteFormat = format == ProxyConfigurationNormalizeFormat.Yaml
            ? SiteConfigurationFormat.Yaml
            : SiteConfigurationFormat.Json;

        try
        {
            var site = _siteParser.ReadSiteText(text, siteFormat);
            var canonicalJson = site is null
                ? null
                : JsonSerializer.Serialize(site, SiteConfigurationParser.WriteJsonOptions);
            return ProxyConfigurationNormalizeSiteParseResult.Parsed(site, canonicalJson);
        }
        catch (JsonException exception)
        {
            return ProxyConfigurationNormalizeSiteParseResult.Failed($"JSON is invalid: {exception.Message}");
        }
        catch (YamlException exception)
        {
            return ProxyConfigurationNormalizeSiteParseResult.Failed($"YAML is invalid: {exception.Message}");
        }
    }
}
