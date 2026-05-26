using System.Text.Json;
using MDRAVA.API.Proxy.Configuration;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Configuration.Loading;
using Microsoft.Extensions.Options;
using YamlDotNet.Core;

namespace MDRAVA.API.Proxy.Diagnostics;

public sealed class ProxyConfigLintSubmittedConfigurationSource
    : IProxyConfigLintSubmittedConfigurationSource
{
    private readonly SiteConfigurationParser _siteParser;
    private readonly IValidateOptions<ProxyOptions> _validator;

    public ProxyConfigLintSubmittedConfigurationSource(
        SiteConfigurationParser siteParser,
        IValidateOptions<ProxyOptions> validator)
    {
        _siteParser = siteParser;
        _validator = validator;
    }

    public ProxyConfigLintSubmittedConfigurationResult Read(
        ConfigLintRequest request,
        ProxyConfigurationNormalizeFormat format,
        DateTimeOffset loadedAtUtc)
    {
        var siteFormat = format == ProxyConfigurationNormalizeFormat.Yaml
            ? SiteConfigurationFormat.Yaml
            : SiteConfigurationFormat.Json;

        SiteOptions? site;
        try
        {
            site = _siteParser.ReadSiteText(request.Text, siteFormat);
        }
        catch (JsonException exception)
        {
            return Failure(ProxyConfigLintSubmittedConfigurationFailureKind.JsonParseError, exception.Message);
        }
        catch (YamlException exception)
        {
            return Failure(ProxyConfigLintSubmittedConfigurationFailureKind.YamlParseError, exception.Message);
        }

        if (site is null)
        {
            return Failure(ProxyConfigLintSubmittedConfigurationFailureKind.EmptySite, null);
        }

        var options = SiteOptionsAggregator.ToProxyOptions([new SiteConfigurationSource("lint-input", site)]);
        var validation = _validator.Validate(null, options);
        var validationErrors = validation.Failed
            ? validation.Failures.Select(static failure => new ProxyConfigurationFileError("lint-input", failure)).ToArray()
            : [];

        var snapshot = ProxyConfigurationMapper.ToRuntimeSnapshot(
            options,
            new ProxyOperationalOptions(),
            new Dictionary<string, RuntimeCertificate>(StringComparer.OrdinalIgnoreCase),
            version: 0,
            loadedAtUtc: loadedAtUtc,
            sourceDirectory: "submitted",
            sourceFiles: ["lint-input"],
            discovery: new ProxyConfigurationDiscovery(
                new ProxyFilesystemLayout("", "", "", "", "", "", ""),
                [new ProxyConfigurationFileDiscovery("lint-input", FormatName(format), "submitted", "Submitted lint input.")],
                [],
                []));

        return new ProxyConfigLintSubmittedConfigurationResult(
            ProxyConfigLintConfigurationSnapshotMapper.ToLintSnapshot(snapshot),
            validationErrors,
            null);
    }

    private static ProxyConfigLintSubmittedConfigurationResult Failure(
        ProxyConfigLintSubmittedConfigurationFailureKind kind,
        string? message)
    {
        return new ProxyConfigLintSubmittedConfigurationResult(
            null,
            [],
            new ProxyConfigLintSubmittedConfigurationFailure(kind, message));
    }

    private static string FormatName(ProxyConfigurationNormalizeFormat format)
    {
        return format == ProxyConfigurationNormalizeFormat.Yaml ? "yaml" : "json";
    }
}
