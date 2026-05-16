using System.Text.Json;
using MDRAVA.API.Proxy.Configuration.Paths;
using MDRAVA.API.Proxy.Configuration.Runtime;
using Microsoft.Extensions.Options;

namespace MDRAVA.API.Proxy.Configuration.Loading;

public sealed class ProxyConfigurationLoader : IProxyConfigurationLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly IMdravaDataDirectoryProvider _dataDirectoryProvider;
    private readonly IValidateOptions<ProxyOptions> _validator;
    private int _nextVersion;

    public ProxyConfigurationLoader(
        IMdravaDataDirectoryProvider dataDirectoryProvider,
        IValidateOptions<ProxyOptions> validator)
    {
        _dataDirectoryProvider = dataDirectoryProvider;
        _validator = validator;
    }

    public async ValueTask<ProxyConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        var sourceDirectory = _dataDirectoryProvider.GetSitesConfigDirectory();

        if (!Directory.Exists(sourceDirectory))
        {
            return ProxyConfigurationLoadResult.Failure(
                sourceDirectory,
                [$"Proxy site configuration directory does not exist: {sourceDirectory}"]);
        }

        var siteFiles = Directory
            .EnumerateFiles(sourceDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (siteFiles.Length == 0)
        {
            return ProxyConfigurationLoadResult.Failure(
                sourceDirectory,
                [$"Proxy site configuration directory contains no .json files: {sourceDirectory}"]);
        }

        List<SiteConfigurationSource> sites = [];
        List<string> errors = [];

        foreach (var siteFile in siteFiles)
        {
            var site = await ReadSiteAsync(siteFile, errors, cancellationToken);
            if (site is not null)
            {
                sites.Add(new SiteConfigurationSource(siteFile, site));
            }
        }

        if (errors.Count > 0)
        {
            return ProxyConfigurationLoadResult.Failure(
                sourceDirectory,
                errors);
        }

        var options = SiteOptionsAggregator.ToProxyOptions(sites);
        var validation = _validator.Validate(null, options);
        if (validation.Failed)
        {
            return ProxyConfigurationLoadResult.Failure(
                sourceDirectory,
                validation.Failures.ToArray());
        }

        var version = Interlocked.Increment(ref _nextVersion);
        var snapshot = ProxyConfigurationMapper.ToRuntimeSnapshot(
            options,
            version,
            DateTimeOffset.UtcNow,
            sourceDirectory,
            siteFiles);

        return ProxyConfigurationLoadResult.Success(sourceDirectory, snapshot);
    }

    private static async ValueTask<SiteOptions?> ReadSiteAsync(
        string siteFile,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(siteFile);
            var site = await JsonSerializer.DeserializeAsync<SiteOptions>(
                stream,
                JsonOptions,
                cancellationToken);

            if (site is null)
            {
                errors.Add($"{siteFile}: site configuration did not contain a JSON object.");
                return null;
            }

            return site;
        }
        catch (JsonException exception)
        {
            errors.Add($"{siteFile}: JSON is invalid: {exception.Message}");
            return null;
        }
        catch (IOException exception)
        {
            errors.Add($"{siteFile}: file could not be read: {exception.Message}");
            return null;
        }
        catch (UnauthorizedAccessException exception)
        {
            errors.Add($"{siteFile}: file could not be accessed: {exception.Message}");
            return null;
        }
    }
}
