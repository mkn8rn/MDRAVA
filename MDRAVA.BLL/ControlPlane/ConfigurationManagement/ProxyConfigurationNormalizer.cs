using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public sealed class ProxyConfigurationNormalizer
    : IProxyConfigurationNormalizeOperations
{
    private readonly IProxyConfigurationNormalizeSiteParser _siteParser;
    private readonly IProxyEndpointAddressPolicy _endpointAddressPolicy;
    private readonly IProxyUrlSyntaxPolicy _urlSyntaxPolicy;

    public ProxyConfigurationNormalizer(
        IProxyConfigurationNormalizeSiteParser siteParser,
        IProxyEndpointAddressPolicy endpointAddressPolicy,
        IProxyUrlSyntaxPolicy urlSyntaxPolicy)
    {
        _siteParser = siteParser;
        _endpointAddressPolicy = endpointAddressPolicy;
        _urlSyntaxPolicy = urlSyntaxPolicy;
    }

    public ProxyConfigurationNormalizeResult Normalize(ProxyConfigurationNormalizeRequest? request)
    {
        if (request is null)
        {
            return Failure(
                "unknown",
                [new ProxyConfigurationFileError(null, "A normalize request body is required.")]);
        }

        if (!TryParseFormat(request.Format, out var format))
        {
            var error = new ProxyConfigurationFileError(null, "Format must be 'json' or 'yaml'.");
            return Failure(RequestFormatName(request.Format), [error]);
        }

        var formatName = FormatName(format);
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return Failure(formatName, [new ProxyConfigurationFileError(null, "Submitted config text is required.")]);
        }

        var parsed = _siteParser.Parse(request.Text, format);
        if (!parsed.Succeeded)
        {
            return Failure(formatName, [new ProxyConfigurationFileError(null, parsed.Error ?? "Site configuration is invalid.")]);
        }

        if (parsed.Site is null)
        {
            return Failure(formatName, [new ProxyConfigurationFileError(null, "Site configuration did not contain an object.")]);
        }

        var options = SiteOptionsAggregator.ToProxyOptions(
            [new SiteConfigurationSource("normalize-input", parsed.Site)]);
        var validationFailures = ProxyOptionsValidationRules.Validate(options, _endpointAddressPolicy, _urlSyntaxPolicy);
        if (validationFailures.Count > 0)
        {
            return Failure(
                formatName,
                validationFailures
                    .Select(static failure => new ProxyConfigurationFileError(null, failure))
                    .ToArray());
        }

        return new ProxyConfigurationNormalizeResult(
            true,
            formatName,
            parsed.CanonicalJson,
            [],
            []);
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

    private static bool TryParseFormat(
        string? format,
        out ProxyConfigurationNormalizeFormat parsed)
    {
        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            parsed = ProxyConfigurationNormalizeFormat.Json;
            return true;
        }

        if (string.Equals(format, "yaml", StringComparison.OrdinalIgnoreCase)
            || string.Equals(format, "yml", StringComparison.OrdinalIgnoreCase))
        {
            parsed = ProxyConfigurationNormalizeFormat.Yaml;
            return true;
        }

        parsed = ProxyConfigurationNormalizeFormat.Json;
        return false;
    }

    private static string FormatName(ProxyConfigurationNormalizeFormat format)
    {
        return format == ProxyConfigurationNormalizeFormat.Yaml ? "yaml" : "json";
    }

    private static string RequestFormatName(string? format)
    {
        return string.IsNullOrWhiteSpace(format) ? "unknown" : format;
    }
}
