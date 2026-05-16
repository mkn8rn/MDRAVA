using System.Text.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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

        var listenerMergeFailures = ValidateListenerMergeCompatibility(sites);
        if (listenerMergeFailures.Count > 0)
        {
            return ProxyConfigurationLoadResult.Failure(
                sourceDirectory,
                listenerMergeFailures);
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
        var certificateValidationFailures = ValidateTlsReferences(options, operationalOptions);
        if (certificateValidationFailures.Count > 0)
        {
            return ProxyConfigurationLoadResult.Failure(
                sourceDirectory,
                certificateValidationFailures);
        }

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

        var certificates = LoadCertificates(operationalOptions, _dataDirectoryProvider.GetDataDirectory(), errors);
        if (errors.Count > 0)
        {
            return ProxyConfigurationLoadResult.Failure(
                sourceDirectory,
                errors);
        }

        var version = Interlocked.Increment(ref _nextVersion);
        var snapshot = ProxyConfigurationMapper.ToRuntimeSnapshot(
            options,
            operationalOptions,
            certificates,
            version,
            DateTimeOffset.UtcNow,
            sourceDirectory,
            siteFiles);

        return ProxyConfigurationLoadResult.Success(sourceDirectory, snapshot);
    }

    private static IReadOnlyList<string> ValidateTlsReferences(
        ProxyOptions options,
        ProxyOperationalOptions operationalOptions)
    {
        List<string> failures = [];
        HashSet<string> certificateIds = operationalOptions.Certificates
            .Select(static certificate => certificate.Id)
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var listenerIndex = 0; listenerIndex < options.Listeners.Count; listenerIndex++)
        {
            var listener = options.Listeners[listenerIndex];
            var prefix = $"Proxy:Listeners:{listenerIndex}";
            var isHttp = string.Equals(listener.Transport, "http", StringComparison.OrdinalIgnoreCase);
            var isHttps = string.Equals(listener.Transport, "https", StringComparison.OrdinalIgnoreCase);

            if (!isHttp && !isHttps)
            {
                failures.Add($"{prefix}:Transport must be 'http' or 'https'.");
                continue;
            }

            if (isHttp)
            {
                if (!string.IsNullOrWhiteSpace(listener.DefaultCertificateId) || listener.SniCertificates.Count > 0)
                {
                    failures.Add($"{prefix} must not configure certificates when Transport is 'http'.");
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(listener.DefaultCertificateId) && listener.SniCertificates.Count == 0)
            {
                failures.Add($"{prefix} must configure DefaultCertificateId or SniCertificates when Transport is 'https'.");
            }

            if (!string.IsNullOrWhiteSpace(listener.DefaultCertificateId)
                && !certificateIds.Contains(listener.DefaultCertificateId))
            {
                failures.Add($"{prefix}:DefaultCertificateId references unknown certificate '{listener.DefaultCertificateId}'.");
            }

            HashSet<string> sniHosts = new(StringComparer.OrdinalIgnoreCase);
            for (var bindingIndex = 0; bindingIndex < listener.SniCertificates.Count; bindingIndex++)
            {
                var binding = listener.SniCertificates[bindingIndex];
                var bindingPrefix = $"{prefix}:SniCertificates:{bindingIndex}";
                if (string.IsNullOrWhiteSpace(binding.HostName))
                {
                    failures.Add($"{bindingPrefix}:HostName is required.");
                }
                else if (!sniHosts.Add(binding.HostName))
                {
                    failures.Add($"{bindingPrefix}:HostName '{binding.HostName}' is duplicated for this listener.");
                }

                if (string.IsNullOrWhiteSpace(binding.CertificateId))
                {
                    failures.Add($"{bindingPrefix}:CertificateId is required.");
                }
                else if (!certificateIds.Contains(binding.CertificateId))
                {
                    failures.Add($"{bindingPrefix}:CertificateId references unknown certificate '{binding.CertificateId}'.");
                }
            }
        }

        return failures;
    }

    private static IReadOnlyList<string> ValidateListenerMergeCompatibility(IReadOnlyList<SiteConfigurationSource> sites)
    {
        List<string> failures = [];
        Dictionary<string, string> defaultCertificatesByListener = new(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sites)
        {
            foreach (var listener in source.Site.Listeners)
            {
                if (string.IsNullOrWhiteSpace(listener.DefaultCertificateId))
                {
                    continue;
                }

                var key = $"{listener.Name}|{listener.Address}|{listener.Port}|{listener.Transport}";
                if (defaultCertificatesByListener.TryGetValue(key, out var existingCertificateId)
                    && !string.Equals(existingCertificateId, listener.DefaultCertificateId, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add(
                        $"{source.Path}: listener '{listener.Name}' has default certificate '{listener.DefaultCertificateId}', but another site on the same listener uses '{existingCertificateId}'.");
                }
                else
                {
                    defaultCertificatesByListener[key] = listener.DefaultCertificateId;
                }
            }
        }

        return failures;
    }

    private static IReadOnlyDictionary<string, RuntimeCertificate> LoadCertificates(
        ProxyOperationalOptions operationalOptions,
        string dataDirectory,
        List<string> errors)
    {
        Dictionary<string, RuntimeCertificate> certificates = new(StringComparer.OrdinalIgnoreCase);

        foreach (var certificateOptions in operationalOptions.Certificates)
        {
            if (string.IsNullOrWhiteSpace(certificateOptions.Id)
                || !string.Equals(certificateOptions.Format, "pfx", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(certificateOptions.Path))
            {
                continue;
            }

            var certificatePath = Path.IsPathRooted(certificateOptions.Path)
                ? certificateOptions.Path
                : Path.Combine(dataDirectory, certificateOptions.Path);

            if (!File.Exists(certificatePath))
            {
                errors.Add($"Certificate '{certificateOptions.Id}' file does not exist: {certificatePath}");
                continue;
            }

            var password = ResolveCertificatePassword(certificateOptions);
            var hasConfiguredPassword = !string.IsNullOrEmpty(certificateOptions.Password)
                || !string.IsNullOrWhiteSpace(certificateOptions.PasswordEnvironmentVariable);
            try
            {
                var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                    certificatePath,
                    password,
                    X509KeyStorageFlags.UserKeySet);
                if (!certificate.HasPrivateKey)
                {
                    certificate.Dispose();
                    errors.Add($"Certificate '{certificateOptions.Id}' must contain a private key.");
                    continue;
                }

                certificates.Add(
                    certificateOptions.Id,
                    new RuntimeCertificate(
                        certificateOptions.Id,
                        certificatePath,
                        "pfx",
                        hasConfiguredPassword,
                        certificate));
            }
            catch (CryptographicException exception)
            {
                errors.Add($"Certificate '{certificateOptions.Id}' could not be loaded from '{certificatePath}': {exception.Message}");
            }
        }

        return certificates;
    }

    private static ReadOnlySpan<char> ResolveCertificatePassword(CertificateOptions certificateOptions)
    {
        if (!string.IsNullOrWhiteSpace(certificateOptions.PasswordEnvironmentVariable))
        {
            return Environment.GetEnvironmentVariable(certificateOptions.PasswordEnvironmentVariable).AsSpan();
        }

        return certificateOptions.Password.AsSpan();
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
