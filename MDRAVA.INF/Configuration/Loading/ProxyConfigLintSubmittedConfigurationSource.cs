using MDRAVA.BLL.ControlPlane.ConfigurationManagement;
using System.Text.Json;
using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.ConfigLint;
using YamlDotNet.Core;

namespace MDRAVA.INF.Configuration.Loading;

public sealed class ProxyConfigLintSubmittedConfigurationSource
    : IProxyConfigLintSubmittedConfigurationSource
{
    private readonly SiteConfigurationParser _siteParser;

    public ProxyConfigLintSubmittedConfigurationSource(SiteConfigurationParser siteParser)
    {
        _siteParser = siteParser;
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
        var validationErrors = ProxyOptionsValidationRules.Validate(options)
            .Select(static failure => new ProxyConfigurationFileError("lint-input", failure))
            .ToArray();

        var operationalOptions = new ProxyOperationalOptions();
        var snapshot = ProxyConfigurationRuntimeMapper.ToRuntimeSnapshot(
            options,
            operationalOptions,
            ProxyAdminSecurityTokenPolicy.Resolve(operationalOptions.Admin, static _ => null),
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
