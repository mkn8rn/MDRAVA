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
    private readonly ILogger<ProxyConfigurationLoader> _logger;
    private int _nextVersion;

    public ProxyConfigurationLoader(
        IMdravaDataDirectoryProvider dataDirectoryProvider,
        IValidateOptions<ProxyOptions> validator,
        ILogger<ProxyConfigurationLoader> logger)
    {
        _dataDirectoryProvider = dataDirectoryProvider;
        _validator = validator;
        _logger = logger;
    }

    public async ValueTask<ProxyConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        var sourceDirectory = _dataDirectoryProvider.GetSitesConfigDirectory();
        var operationalConfigPath = _dataDirectoryProvider.GetProxyOperationalConfigPath();

        Directory.CreateDirectory(sourceDirectory);

        var siteFiles = Directory
            .EnumerateFiles(sourceDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

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

        var operationalOptions = await ReadOperationalOptionsAsync(operationalConfigPath, errors, cancellationToken);
        if (errors.Count > 0)
        {
            return ProxyConfigurationLoadResult.Failure(
                sourceDirectory,
                errors);
        }

        var operationalFailures = ProxyOperationalOptionsValidator.Validate(operationalOptions);
        if (operationalFailures.Count > 0)
        {
            return ProxyConfigurationLoadResult.Failure(
                sourceDirectory,
                operationalFailures);
        }

        var options = SiteOptionsAggregator.ToProxyOptions(sites);
        if (siteFiles.Length > 0)
        {
            var validation = _validator.Validate(null, options);
            if (validation.Failed)
            {
                return ProxyConfigurationLoadResult.Failure(
                    sourceDirectory,
                    validation.Failures.ToArray());
            }
        }
        else
        {
            _logger.LogWarning(
                "No proxy site configuration files were found in {SourcePath}; MDRAVA will start with no configured sites, listeners, or routes.",
                sourceDirectory);
        }

        var version = Interlocked.Increment(ref _nextVersion);
        var snapshot = ProxyConfigurationMapper.ToRuntimeSnapshot(
            options,
            operationalOptions,
            version,
            DateTimeOffset.UtcNow,
            sourceDirectory,
            siteFiles);

        return ProxyConfigurationLoadResult.Success(sourceDirectory, snapshot);
    }

    private async ValueTask<ProxyOperationalOptions> ReadOperationalOptionsAsync(
        string operationalConfigPath,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(operationalConfigPath))
        {
            _logger.LogInformation(
                "Proxy operational configuration file {ConfigPath} was not found; using in-memory default timeout settings.",
                operationalConfigPath);
            return new ProxyOperationalOptions();
        }

        try
        {
            await using var stream = File.OpenRead(operationalConfigPath);
            var options = await JsonSerializer.DeserializeAsync<ProxyOperationalOptions>(
                stream,
                JsonOptions,
                cancellationToken);

            if (options is null)
            {
                errors.Add($"{operationalConfigPath}: operational configuration did not contain a JSON object.");
                return new ProxyOperationalOptions();
            }

            return options;
        }
        catch (JsonException exception)
        {
            errors.Add($"{operationalConfigPath}: JSON is invalid: {exception.Message}");
            return new ProxyOperationalOptions();
        }
        catch (IOException exception)
        {
            errors.Add($"{operationalConfigPath}: file could not be read: {exception.Message}");
            return new ProxyOperationalOptions();
        }
        catch (UnauthorizedAccessException exception)
        {
            errors.Add($"{operationalConfigPath}: file could not be accessed: {exception.Message}");
            return new ProxyOperationalOptions();
        }
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
