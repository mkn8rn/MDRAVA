using System.Text.Json;
using Microsoft.Extensions.Options;
using YamlDotNet.Core;

namespace MDRAVA.API.Proxy.Configuration.Loading;

public sealed class ProxyConfigurationNormalizer : IProxyConfigurationNormalizer
{
    private readonly SiteConfigurationParser _siteParser;
    private readonly IValidateOptions<ProxyOptions> _validator;

    public ProxyConfigurationNormalizer(
        SiteConfigurationParser siteParser,
        IValidateOptions<ProxyOptions> validator)
    {
        _siteParser = siteParser;
        _validator = validator;
    }

    public ProxyConfigurationNormalizeResult Normalize(ProxyConfigurationNormalizeRequest request)
    {
        if (!SiteConfigurationFileDiscovery.TryParseFormat(request.Format, out var format))
        {
            var error = new ProxyConfigurationFileError(null, "Format must be 'json' or 'yaml'.");
            return Failure(request.Format, [error]);
        }

        try
        {
            var site = _siteParser.ReadSiteText(request.Text, format);
            if (site is null)
            {
                return Failure(request.Format, [new ProxyConfigurationFileError(null, "Site configuration did not contain an object.")]);
            }

            var options = SiteOptionsAggregator.ToProxyOptions([new SiteConfigurationSource("normalize-input", site)]);
            var validation = _validator.Validate(null, options);
            if (validation.Failed)
            {
                return Failure(
                    SiteConfigurationFileDiscovery.FormatName(format),
                    validation.Failures
                        .Select(static failure => new ProxyConfigurationFileError(null, failure))
                        .ToArray());
            }

            var canonicalJson = JsonSerializer.Serialize(site, SiteConfigurationParser.WriteJsonOptions);
            return new ProxyConfigurationNormalizeResult(
                true,
                SiteConfigurationFileDiscovery.FormatName(format),
                canonicalJson,
                [],
                []);
        }
        catch (JsonException exception)
        {
            return Failure(SiteConfigurationFileDiscovery.FormatName(format), [new ProxyConfigurationFileError(null, $"JSON is invalid: {exception.Message}")]);
        }
        catch (YamlException exception)
        {
            return Failure(SiteConfigurationFileDiscovery.FormatName(format), [new ProxyConfigurationFileError(null, $"YAML is invalid: {exception.Message}")]);
        }
    }

    private static ProxyConfigurationNormalizeResult Failure(
        string format,
        IReadOnlyList<ProxyConfigurationFileError> errors)
    {
        return new ProxyConfigurationNormalizeResult(
            false,
            format,
            null,
            errors.Select(static error => error.Path is null ? error.Message : $"{error.Path}: {error.Message}").ToArray(),
            errors);
    }
}
