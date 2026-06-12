using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;
using MDRAVA.BLL.ControlPlane.Status;

namespace MDRAVA.BLL.ControlPlane.Backup;

public sealed class ProxyBackupService : IProxyBackupOperations
{
    private const int MaxEntries = 256;
    private const int MaxWarnings = 64;

    private readonly IMdravaDataDirectoryProvider _dataDirectoryProvider;
    private readonly IProxyBackupFileSystem _backupFileSystem;
    private readonly IProxyRestoreConfigurationValidator _restoreConfigurationValidator;
    private readonly IProxyActiveConfigurationVersionReader _activeConfigurationVersionReader;
    private readonly TimeProvider _timeProvider;

    public ProxyBackupService(
        IMdravaDataDirectoryProvider dataDirectoryProvider,
        IProxyBackupFileSystem backupFileSystem,
        IProxyRestoreConfigurationValidator restoreConfigurationValidator,
        IProxyActiveConfigurationVersionReader activeConfigurationVersionReader,
        TimeProvider timeProvider)
    {
        _dataDirectoryProvider = dataDirectoryProvider;
        _backupFileSystem = backupFileSystem;
        _restoreConfigurationValidator = restoreConfigurationValidator;
        _activeConfigurationVersionReader = activeConfigurationVersionReader;
        _timeProvider = timeProvider;
    }

    public ProxyBackupManifestResponse CreateManifest()
    {
        var generatedAtUtc = _timeProvider.GetUtcNow();
        var root = _dataDirectoryProvider.GetDataDirectory();
        var directories = ExpectedDirectories(root);
        List<ProxyBackupManifestEntry> entries = [];
        List<ProxyBackupWarning> warnings = [];

        var scan = _backupFileSystem.ScanDataDirectory(root);
        if (!scan.RootExists)
        {
            warnings.Add(ProxyBackupWarningPolicy.DataDirectoryMissing());
        }
        else
        {
            foreach (var file in scan.Files)
            {
                var category = ProxyBackupFileClassificationPolicy.ClassifyFile(file.RelativePath);
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
                warnings.Add(ProxyBackupWarningPolicy.FromFileSystemWarning(warning));
            }
        }

        foreach (var directory in directories.Where(static directory => !directory.Exists))
        {
            warnings.Add(ProxyBackupWarningPolicy.MissingDirectory(directory.RelativePath));
        }

        return ProxyBackupManifestBuilder.Build(
            generatedAtUtc,
            directories,
            entries,
            warnings,
            MaxEntries,
            MaxWarnings);
    }

    public async ValueTask<ProxyRestoreValidationResponse> ValidateAsync(CancellationToken cancellationToken)
    {
        var generatedAtUtc = _timeProvider.GetUtcNow();
        var root = _dataDirectoryProvider.GetDataDirectory();
        var manifest = CreateManifest();
        List<ProxyRestoreValidationFinding> errors = [];
        List<ProxyRestoreValidationFinding> warnings = manifest.Warnings
            .Select(ProxyRestoreValidationFindingPolicy.FromBackupWarning)
            .ToList();

        errors.AddRange(ProxyRestoreValidationDirectoryPolicy.FindMissingRequiredDirectories(manifest.Directories));

        var configValidation = await _restoreConfigurationValidator.ValidateExistingLayoutAsync(cancellationToken);
        foreach (var fileError in configValidation.FileErrors)
        {
            errors.Add(ClassifyConfigError(root, fileError));
        }

        foreach (var error in configValidation.Errors.Except(configValidation.FileErrors.Select(static fileError => fileError.Message)))
        {
            var finding = ProxyRestoreValidationFindingPolicy.ClassifyConfigurationError(error);
            errors.Add(new ProxyRestoreValidationFinding(
                ProxyStatusText.Error,
                finding.Code,
                finding.Message,
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
        return ProxyBackupDirectoryLayoutPolicy.ExpectedDirectories()
            .Select(requirement => DirectoryStatus(root, requirement))
            .ToArray();
    }

    private ProxyBackupDirectoryStatus DirectoryStatus(
        string root,
        ProxyBackupDirectoryRequirement requirement)
    {
        return new ProxyBackupDirectoryStatus(
            requirement.RelativePath,
            _backupFileSystem.DirectoryExists(root, requirement.RelativePath),
            requirement.Classification,
            requirement.Sensitive);
    }

    private ProxyRestoreValidationFinding ClassifyConfigError(string root, ProxyConfigurationFileError fileError)
    {
        var finding = ProxyRestoreValidationFindingPolicy.ClassifyConfigurationError(fileError.Message);
        return new ProxyRestoreValidationFinding(
            ProxyStatusText.Error,
            finding.Code,
            finding.Message,
            SafeRelativeOrNull(root, fileError.Path));
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

}
