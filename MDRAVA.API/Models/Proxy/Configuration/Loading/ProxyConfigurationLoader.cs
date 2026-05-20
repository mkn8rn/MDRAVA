using System.Text.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using MDRAVA.API.Proxy.Acme;
using MDRAVA.API.Proxy.Configuration.Paths;
using MDRAVA.API.Proxy.Configuration.Runtime;
using Microsoft.Extensions.Options;
using YamlDotNet.Core;

namespace MDRAVA.API.Proxy.Configuration.Loading;

public sealed class ProxyConfigurationLoader : IProxyConfigurationLoader
{
    private readonly IMdravaDataDirectoryProvider _dataDirectoryProvider;
    private readonly IValidateOptions<ProxyOptions> _validator;
    private readonly ProxyDataDirectoryBootstrapper _bootstrapper;
    private readonly SiteConfigurationParser _siteParser;
    private readonly ILogger<ProxyConfigurationLoader> _logger;
    private int _nextVersion;

    public ProxyConfigurationLoader(
        IMdravaDataDirectoryProvider dataDirectoryProvider,
        IValidateOptions<ProxyOptions> validator,
        ProxyDataDirectoryBootstrapper bootstrapper,
        SiteConfigurationParser siteParser,
        ILogger<ProxyConfigurationLoader> logger)
    {
        _dataDirectoryProvider = dataDirectoryProvider;
        _validator = validator;
        _bootstrapper = bootstrapper;
        _siteParser = siteParser;
        _logger = logger;
    }

    public async ValueTask<ProxyConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        return await LoadCoreAsync(allocateVersion: true, ensureLayout: true, cancellationToken);
    }

    public async ValueTask<ProxyConfigurationLoadResult> ValidateAsync(CancellationToken cancellationToken)
    {
        return await LoadCoreAsync(allocateVersion: false, ensureLayout: true, cancellationToken);
    }

    public async ValueTask<ProxyConfigurationLoadResult> ValidateExistingLayoutAsync(CancellationToken cancellationToken)
    {
        return await LoadCoreAsync(allocateVersion: false, ensureLayout: false, cancellationToken);
    }

    private async ValueTask<ProxyConfigurationLoadResult> LoadCoreAsync(
        bool allocateVersion,
        bool ensureLayout,
        CancellationToken cancellationToken)
    {
        var sourceDirectory = _dataDirectoryProvider.GetSitesConfigDirectory();
        var operationalConfigPath = _dataDirectoryProvider.GetProxyOperationalConfigPath();
        var attemptedAtUtc = DateTimeOffset.UtcNow;
        var wouldBeVersion = Volatile.Read(ref _nextVersion) + 1;
        var bootstrapDiscovery = ensureLayout ? _bootstrapper.EnsureLayout() : _bootstrapper.InspectLayout();
        List<ProxyConfigurationFileDiscovery> discoveredFiles = [.. bootstrapDiscovery.Files];

        var discoveredSiteFiles = SiteConfigurationFileDiscovery.DiscoverLoadableSiteFiles(
            sourceDirectory,
            discoveredFiles);
        var siteFiles = discoveredSiteFiles.Select(static file => file.Path).ToArray();

        List<SiteConfigurationSource> sites = [];
        List<ProxyConfigurationFileError> errors = [];

        foreach (var (siteFile, format) in discoveredSiteFiles)
        {
            var site = await ReadSiteAsync(siteFile, format, discoveredFiles, errors, cancellationToken);
            if (site is not null)
            {
                sites.Add(new SiteConfigurationSource(siteFile, site));
            }
        }

        ProxyConfigurationDiscovery BuildDiscovery()
        {
            return bootstrapDiscovery with { Files = discoveredFiles.ToArray() };
        }

        if (errors.Count > 0)
        {
            return ProxyConfigurationLoadResult.Failure(
                sourceDirectory,
                attemptedAtUtc,
                siteFiles,
                BuildDiscovery(),
                errors,
                wouldBeVersion);
        }

        var listenerMergeFailures = ValidateListenerMergeCompatibility(sites);
        if (listenerMergeFailures.Count > 0)
        {
            return ProxyConfigurationLoadResult.Failure(
                sourceDirectory,
                attemptedAtUtc,
                siteFiles,
                BuildDiscovery(),
                listenerMergeFailures.Select(static failure => new ProxyConfigurationFileError(null, failure)).ToArray(),
                wouldBeVersion);
        }

        var operationalOptions = await ReadOperationalOptionsAsync(operationalConfigPath, discoveredFiles, errors, cancellationToken);
        if (errors.Count > 0)
        {
            return ProxyConfigurationLoadResult.Failure(
                sourceDirectory,
                attemptedAtUtc,
                siteFiles,
                BuildDiscovery(),
                errors,
                wouldBeVersion);
        }

        var operationalFailures = ProxyOperationalOptionsValidator.Validate(operationalOptions);
        if (operationalFailures.Count > 0)
        {
            return ProxyConfigurationLoadResult.Failure(
                sourceDirectory,
                attemptedAtUtc,
                siteFiles,
                BuildDiscovery(),
                operationalFailures.Select(failure => new ProxyConfigurationFileError(operationalConfigPath, failure)).ToArray(),
                wouldBeVersion);
        }

        var options = SiteOptionsAggregator.ToProxyOptions(sites);
        var certificateValidationFailures = ValidateTlsReferences(options, operationalOptions);
        if (certificateValidationFailures.Count > 0)
        {
            return ProxyConfigurationLoadResult.Failure(
                sourceDirectory,
                attemptedAtUtc,
                siteFiles,
                BuildDiscovery(),
                certificateValidationFailures.Select(static failure => new ProxyConfigurationFileError(null, failure)).ToArray(),
                wouldBeVersion);
        }

        if (siteFiles.Length > 0)
        {
            var validation = _validator.Validate(null, options);
            if (validation.Failed)
            {
                return ProxyConfigurationLoadResult.Failure(
                    sourceDirectory,
                    attemptedAtUtc,
                    siteFiles,
                    BuildDiscovery(),
                    validation.Failures.Select(static failure => new ProxyConfigurationFileError(null, failure)).ToArray(),
                    wouldBeVersion);
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
            DisposeCertificates(certificates);
            return ProxyConfigurationLoadResult.Failure(
                sourceDirectory,
                attemptedAtUtc,
                siteFiles,
                BuildDiscovery(),
                errors,
                wouldBeVersion);
        }

        if (!allocateVersion)
        {
            DisposeCertificates(certificates);
            return new ProxyConfigurationLoadResult(
                true,
                sourceDirectory,
                attemptedAtUtc,
                siteFiles,
                BuildDiscovery(),
                null,
                [],
                [],
                wouldBeVersion);
        }

        var version = Interlocked.Increment(ref _nextVersion);
        var snapshot = ProxyConfigurationMapper.ToRuntimeSnapshot(
            options,
            operationalOptions,
            certificates,
            version,
            DateTimeOffset.UtcNow,
            sourceDirectory,
            siteFiles,
            BuildDiscovery());

        return ProxyConfigurationLoadResult.Success(sourceDirectory, snapshot, BuildDiscovery());
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
        if (operationalOptions.Acme.Enabled)
        {
            foreach (var acmeCertificateId in operationalOptions.Acme.Certificates
                .Where(static certificate => certificate.Enabled)
                .Select(static certificate => certificate.Id)
                .Where(static id => !string.IsNullOrWhiteSpace(id)))
            {
                certificateIds.Add(acmeCertificateId);
            }
        }

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
        List<ProxyConfigurationFileError> errors)
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
                errors.Add(new ProxyConfigurationFileError(null, $"Certificate '{certificateOptions.Id}' file does not exist: {certificatePath}"));
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
                    errors.Add(new ProxyConfigurationFileError(null, $"Certificate '{certificateOptions.Id}' must contain a private key."));
                    continue;
                }

                certificates.Add(
                    certificateOptions.Id,
                    new RuntimeCertificate(
                        certificateOptions.Id,
                        certificatePath,
                        "pfx",
                        hasConfiguredPassword,
                        certificate,
                        "manualPfx",
                        []));
            }
            catch (CryptographicException exception)
            {
                errors.Add(new ProxyConfigurationFileError(null, $"Certificate '{certificateOptions.Id}' could not be loaded from '{certificatePath}': {exception.Message}"));
            }
        }

        foreach (var acmeCertificate in AcmeCertificateMaterialStore.LoadRuntimeCertificates(
            operationalOptions.Acme,
            dataDirectory,
            errors))
        {
            certificates[acmeCertificate.Key] = acmeCertificate.Value;
        }

        return certificates;
    }

    private static void DisposeCertificates(IReadOnlyDictionary<string, RuntimeCertificate> certificates)
    {
        foreach (var certificate in certificates.Values)
        {
            certificate.Certificate.Dispose();
        }
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
        List<ProxyConfigurationFileDiscovery> discoveries,
        List<ProxyConfigurationFileError> errors,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(operationalConfigPath))
        {
            discoveries.Add(new ProxyConfigurationFileDiscovery(
                operationalConfigPath,
                "json",
                "skipped",
                "Proxy operational configuration file does not exist; defaults are used."));
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
                SiteConfigurationParser.ReadJsonOptions,
                cancellationToken);

            if (options is null)
            {
                errors.Add(new ProxyConfigurationFileError(operationalConfigPath, "Operational configuration did not contain a JSON object."));
                discoveries.Add(new ProxyConfigurationFileDiscovery(
                    operationalConfigPath,
                    "json",
                    "failed",
                    "Operational configuration did not contain a JSON object."));
                return new ProxyOperationalOptions();
            }

            discoveries.Add(new ProxyConfigurationFileDiscovery(
                operationalConfigPath,
                "json",
                "loaded",
                "Proxy operational configuration."));
            return options;
        }
        catch (JsonException exception)
        {
            errors.Add(new ProxyConfigurationFileError(operationalConfigPath, $"JSON is invalid: {exception.Message}"));
            discoveries.Add(new ProxyConfigurationFileDiscovery(
                operationalConfigPath,
                "json",
                "failed",
                $"JSON is invalid: {exception.Message}"));
            return new ProxyOperationalOptions();
        }
        catch (IOException exception)
        {
            errors.Add(new ProxyConfigurationFileError(operationalConfigPath, $"File could not be read: {exception.Message}"));
            discoveries.Add(new ProxyConfigurationFileDiscovery(
                operationalConfigPath,
                "json",
                "failed",
                $"File could not be read: {exception.Message}"));
            return new ProxyOperationalOptions();
        }
        catch (UnauthorizedAccessException exception)
        {
            errors.Add(new ProxyConfigurationFileError(operationalConfigPath, $"File could not be accessed: {exception.Message}"));
            discoveries.Add(new ProxyConfigurationFileDiscovery(
                operationalConfigPath,
                "json",
                "failed",
                $"File could not be accessed: {exception.Message}"));
            return new ProxyOperationalOptions();
        }
    }

    private async ValueTask<SiteOptions?> ReadSiteAsync(
        string siteFile,
        SiteConfigurationFormat format,
        List<ProxyConfigurationFileDiscovery> discoveries,
        List<ProxyConfigurationFileError> errors,
        CancellationToken cancellationToken)
    {
        try
        {
            var site = await _siteParser.ReadSiteFileAsync(siteFile, format, cancellationToken);

            if (site is null)
            {
                errors.Add(new ProxyConfigurationFileError(siteFile, "Site configuration did not contain a JSON object."));
                discoveries.Add(new ProxyConfigurationFileDiscovery(
                    siteFile,
                    SiteConfigurationFileDiscovery.FormatName(format),
                    "failed",
                    "Site configuration did not contain a JSON object."));
                return null;
            }

            discoveries.Add(new ProxyConfigurationFileDiscovery(
                siteFile,
                SiteConfigurationFileDiscovery.FormatName(format),
                "loaded",
                "Site configuration."));
            return site;
        }
        catch (JsonException exception)
        {
            errors.Add(new ProxyConfigurationFileError(siteFile, $"JSON is invalid: {exception.Message}"));
            discoveries.Add(new ProxyConfigurationFileDiscovery(
                siteFile,
                SiteConfigurationFileDiscovery.FormatName(format),
                "failed",
                $"JSON is invalid: {exception.Message}"));
            return null;
        }
        catch (YamlException exception)
        {
            errors.Add(new ProxyConfigurationFileError(siteFile, $"YAML is invalid: {exception.Message}"));
            discoveries.Add(new ProxyConfigurationFileDiscovery(
                siteFile,
                SiteConfigurationFileDiscovery.FormatName(format),
                "failed",
                $"YAML is invalid: {exception.Message}"));
            return null;
        }
        catch (IOException exception)
        {
            errors.Add(new ProxyConfigurationFileError(siteFile, $"File could not be read: {exception.Message}"));
            discoveries.Add(new ProxyConfigurationFileDiscovery(
                siteFile,
                SiteConfigurationFileDiscovery.FormatName(format),
                "failed",
                $"File could not be read: {exception.Message}"));
            return null;
        }
        catch (UnauthorizedAccessException exception)
        {
            errors.Add(new ProxyConfigurationFileError(siteFile, $"File could not be accessed: {exception.Message}"));
            discoveries.Add(new ProxyConfigurationFileDiscovery(
                siteFile,
                SiteConfigurationFileDiscovery.FormatName(format),
                "failed",
                $"File could not be accessed: {exception.Message}"));
            return null;
        }
    }
}
