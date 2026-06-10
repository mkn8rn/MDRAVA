using MDRAVA.BLL.ControlPlane.ConfigurationManagement;
using MDRAVA.BLL.ControlPlane.Status;
using MDRAVA.BLL.Infrastructure;

namespace MDRAVA.BLL.ControlPlane.Backup;

public sealed class ProxyBackupService : IProxyBackupOperations
{
    private const int MaxEntries = 256;
    private const int MaxWarnings = 64;
    private const string MustBackup = "must_backup";
    private const string ShouldBackup = "should_backup";
    private const string OptionalBackup = "optional_backup";
    private const string RuntimeGeneratedSafeToOmit = "runtime_generated_safe_to_omit";
    private const string NeverExportByDefaultSensitive = "never_export_by_default_sensitive";

    private readonly IMdravaDataDirectoryProvider _dataDirectoryProvider;
    private readonly IProxyBackupFileSystem _backupFileSystem;
    private readonly IProxyRestoreConfigurationValidator _restoreConfigurationValidator;
    private readonly IProxyActiveConfigurationVersionReader _activeConfigurationVersionReader;

    public ProxyBackupService(
        IMdravaDataDirectoryProvider dataDirectoryProvider,
        IProxyBackupFileSystem backupFileSystem,
        IProxyRestoreConfigurationValidator restoreConfigurationValidator,
        IProxyActiveConfigurationVersionReader activeConfigurationVersionReader)
    {
        _dataDirectoryProvider = dataDirectoryProvider;
        _backupFileSystem = backupFileSystem;
        _restoreConfigurationValidator = restoreConfigurationValidator;
        _activeConfigurationVersionReader = activeConfigurationVersionReader;
    }

    public ProxyBackupManifestResponse CreateManifest()
    {
        var generatedAtUtc = DateTimeOffset.UtcNow;
        var root = _dataDirectoryProvider.GetDataDirectory();
        var directories = ExpectedDirectories(root);
        List<ProxyBackupManifestEntry> entries = [];
        List<ProxyBackupWarning> warnings = [];

        var scan = _backupFileSystem.ScanDataDirectory(root);
        if (!scan.RootExists)
        {
            warnings.Add(new ProxyBackupWarning(
                "data_directory_missing",
                "The MDRAVA data directory does not exist.",
                null));
        }
        else
        {
            foreach (var file in scan.Files)
            {
                var category = ClassifyFile(file.RelativePath);
                entries.Add(new ProxyBackupManifestEntry(
                    file.RelativePath,
                    category.Category,
                    category.Classification,
                    category.Sensitive,
                    file.SizeBytes,
                    file.LastWriteTimeUtc));
            }

            foreach (var warning in scan.Warnings)
            {
                AddWarning(warnings, ToManifestWarning(warning));
            }
        }

        foreach (var directory in directories.Where(static directory => !directory.Exists))
        {
            AddWarning(warnings, new ProxyBackupWarning(
                "missing_directory",
                "An expected data-directory child is missing.",
                directory.RelativePath));
        }

        var truncated = entries.Count > MaxEntries;
        if (truncated)
        {
            entries = entries
                .OrderBy(static entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
                .Take(MaxEntries)
                .ToList();
            AddWarning(warnings, new ProxyBackupWarning(
                "manifest_truncated",
                "The backup manifest reached its bounded entry limit.",
                null));
        }

        var counts = entries
            .GroupBy(static entry => (entry.Category, entry.Classification))
            .Select(static group => new ProxyBackupManifestCount(
                group.Key.Category,
                group.Key.Classification,
                group.Count(),
                group.Sum(static entry => entry.SizeBytes)))
            .OrderBy(static count => count.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static count => count.Classification, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ProxyBackupManifestResponse(
            generatedAtUtc,
            directories,
            entries.OrderBy(static entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray(),
            counts,
            warnings.Take(MaxWarnings).ToArray(),
            truncated);
    }

    public async ValueTask<ProxyRestoreValidationResponse> ValidateAsync(CancellationToken cancellationToken)
    {
        var generatedAtUtc = DateTimeOffset.UtcNow;
        var root = _dataDirectoryProvider.GetDataDirectory();
        var manifest = CreateManifest();
        List<ProxyRestoreValidationFinding> errors = [];
        List<ProxyRestoreValidationFinding> warnings = manifest.Warnings
            .Select(static warning => new ProxyRestoreValidationFinding(
                ProxyStatusText.Warning,
                warning.Code,
                warning.Message,
                warning.RelativePath))
            .ToList();

        foreach (var directory in manifest.Directories.Where(static directory =>
            !directory.Exists
            && string.Equals(directory.Classification, MustBackup, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add(new ProxyRestoreValidationFinding(
                ProxyStatusText.Error,
                "required_directory_missing",
                "A required restore directory is missing.",
                directory.RelativePath));
        }

        var configValidation = await _restoreConfigurationValidator.ValidateExistingLayoutAsync(cancellationToken);
        foreach (var fileError in configValidation.FileErrors)
        {
            errors.Add(ClassifyConfigError(root, fileError));
        }

        foreach (var error in configValidation.Errors.Except(configValidation.FileErrors.Select(static fileError => fileError.Message)))
        {
            errors.Add(new ProxyRestoreValidationFinding(
                ProxyStatusText.Error,
                ClassifyConfigErrorCode(error),
                ClassifyConfigErrorMessage(error),
                null));
        }

        return new ProxyRestoreValidationResponse(
            configValidation.Succeeded && errors.Count == 0,
            generatedAtUtc,
            _activeConfigurationVersionReader.ActiveConfigVersion,
            configValidation.Succeeded,
            configValidation.WouldBeVersion,
            manifest,
            errors.Take(MaxWarnings).ToArray(),
            warnings.Take(MaxWarnings).ToArray());
    }

    private IReadOnlyList<ProxyBackupDirectoryStatus> ExpectedDirectories(string root)
    {
        return
        [
            DirectoryStatus(root, "config", MustBackup, sensitive: false),
            DirectoryStatus(root, "config/sites", MustBackup, sensitive: false),
            DirectoryStatus(root, "logs", ShouldBackup, sensitive: false),
            DirectoryStatus(root, "certs", NeverExportByDefaultSensitive, sensitive: true),
            DirectoryStatus(root, "certs/acme", NeverExportByDefaultSensitive, sensitive: true),
            DirectoryStatus(root, "state", ShouldBackup, sensitive: false)
        ];
    }

    private ProxyBackupDirectoryStatus DirectoryStatus(
        string root,
        string relativePath,
        string classification,
        bool sensitive)
    {
        return new ProxyBackupDirectoryStatus(
            relativePath,
            _backupFileSystem.DirectoryExists(root, relativePath),
            classification,
            sensitive);
    }

    private static (string Category, string Classification, bool Sensitive) ClassifyFile(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        var slash = normalized.LastIndexOf('/');
        var fileName = slash >= 0 ? normalized[(slash + 1)..] : normalized;
        if (string.Equals(fileName, "example.site.yaml", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("example.", StringComparison.OrdinalIgnoreCase))
        {
            return ("generated_example", RuntimeGeneratedSafeToOmit, false);
        }

        if (string.Equals(normalized, "config/proxy.json", StringComparison.OrdinalIgnoreCase))
        {
            return ("config", MustBackup, false);
        }

        if (normalized.StartsWith("config/sites/", StringComparison.OrdinalIgnoreCase))
        {
            return ("site_config", MustBackup, false);
        }

        if (normalized.StartsWith("logs/", StringComparison.OrdinalIgnoreCase))
        {
            return ("logs", ShouldBackup, false);
        }

        if (normalized.StartsWith("certs/acme/accounts/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("certs/acme/private-keys/", StringComparison.OrdinalIgnoreCase))
        {
            return ("acme_secret_material", NeverExportByDefaultSensitive, true);
        }

        if (normalized.StartsWith("certs/acme/certificates/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("certs/acme/metadata/", StringComparison.OrdinalIgnoreCase))
        {
            return ("acme_certificate_state", ShouldBackup, false);
        }

        if (normalized.StartsWith("certs/", StringComparison.OrdinalIgnoreCase)
            && (fileName.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith(".p12", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith(".key", StringComparison.OrdinalIgnoreCase)))
        {
            return ("manual_certificate_material", NeverExportByDefaultSensitive, true);
        }

        if (normalized.StartsWith("certs/", StringComparison.OrdinalIgnoreCase))
        {
            return ("certificate_metadata", ShouldBackup, false);
        }

        if (normalized.StartsWith("state/", StringComparison.OrdinalIgnoreCase))
        {
            return ("state", ShouldBackup, false);
        }

        return ("unknown", OptionalBackup, false);
    }

    private ProxyRestoreValidationFinding ClassifyConfigError(string root, ProxyConfigurationFileError fileError)
    {
        return new ProxyRestoreValidationFinding(
            ProxyStatusText.Error,
            ClassifyConfigErrorCode(fileError.Message),
            ClassifyConfigErrorMessage(fileError.Message),
            SafeRelativeOrNull(root, fileError.Path));
    }

    private static string ClassifyConfigErrorCode(string error)
    {
        if (error.Contains("Certificate", StringComparison.OrdinalIgnoreCase)
            && error.Contains("file does not exist", StringComparison.OrdinalIgnoreCase))
        {
            return "certificate_file_missing";
        }

        if (error.Contains("unknown certificate", StringComparison.OrdinalIgnoreCase))
        {
            return "certificate_reference_missing";
        }

        if (error.Contains("JSON", StringComparison.OrdinalIgnoreCase)
            || error.Contains("YAML", StringComparison.OrdinalIgnoreCase))
        {
            return "config_parse_failed";
        }

        return "config_validation_failed";
    }

    private static string ClassifyConfigErrorMessage(string error)
    {
        return ClassifyConfigErrorCode(error) switch
        {
            "certificate_file_missing" => "Referenced certificate material is missing.",
            "certificate_reference_missing" => "A listener references an unknown configured certificate id.",
            "config_parse_failed" => "A configuration file could not be parsed.",
            _ => "Configuration validation failed."
        };
    }

    private string? SafeRelativeOrNull(string root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return _backupFileSystem.TryGetSafeRelativePath(root, path, out var relativePath)
            ? relativePath
            : null;
    }

    private static ProxyBackupWarning ToManifestWarning(ProxyBackupFileSystemWarning warning)
    {
        return warning.Code switch
        {
            "directory_unreadable" => new ProxyBackupWarning(
                warning.Code,
                "A directory could not be inspected.",
                warning.RelativePath),
            "reparse_point_skipped" => new ProxyBackupWarning(
                warning.Code,
                "A reparse point was skipped during backup manifest generation.",
                warning.RelativePath),
            "unsafe_path_skipped" => new ProxyBackupWarning(
                warning.Code,
                "A file path could not be represented as a safe data-directory relative path.",
                warning.RelativePath),
            _ => new ProxyBackupWarning(
                warning.Code,
                "A backup filesystem path could not be inspected.",
                warning.RelativePath)
        };
    }

    private static void AddWarning(List<ProxyBackupWarning> warnings, ProxyBackupWarning warning)
    {
        if (warnings.Count < MaxWarnings)
        {
            warnings.Add(warning);
        }
    }
}
