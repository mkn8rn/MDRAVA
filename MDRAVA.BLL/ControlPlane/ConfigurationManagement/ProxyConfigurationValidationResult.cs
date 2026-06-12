using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public sealed record ProxyConfigurationValidationResult
{
    private ProxyConfigurationValidationResult(
        bool succeeded,
        string sourceDirectory,
        DateTimeOffset attemptedAtUtc,
        int? activeVersion,
        DateTimeOffset? lastSuccessfulLoadAtUtc,
        int? wouldBeVersion,
        IReadOnlyList<string> sourceFiles,
        ProxyConfigurationDiscovery discovery,
        IReadOnlyList<string> errors,
        IReadOnlyList<ProxyConfigurationFileError> fileErrors)
    {
        Succeeded = succeeded;
        SourceDirectory = sourceDirectory;
        AttemptedAtUtc = attemptedAtUtc;
        ActiveVersion = activeVersion;
        LastSuccessfulLoadAtUtc = lastSuccessfulLoadAtUtc;
        WouldBeVersion = wouldBeVersion;
        SourceFiles = sourceFiles;
        Discovery = discovery;
        Errors = errors;
        FileErrors = fileErrors;
    }

    public bool Succeeded { get; }

    public string SourceDirectory { get; }

    public DateTimeOffset AttemptedAtUtc { get; }

    public int? ActiveVersion { get; }

    public DateTimeOffset? LastSuccessfulLoadAtUtc { get; }

    public int? WouldBeVersion { get; }

    public IReadOnlyList<string> SourceFiles { get; }

    public ProxyConfigurationDiscovery Discovery { get; }

    public IReadOnlyList<string> Errors { get; }

    public IReadOnlyList<ProxyConfigurationFileError> FileErrors { get; }

    public static ProxyConfigurationValidationResult Valid(
        string sourceDirectory,
        DateTimeOffset attemptedAtUtc,
        int? activeVersion,
        DateTimeOffset? lastSuccessfulLoadAtUtc,
        int? wouldBeVersion,
        IReadOnlyList<string> sourceFiles,
        ProxyConfigurationDiscovery discovery)
    {
        return new ProxyConfigurationValidationResult(
            succeeded: true,
            sourceDirectory: sourceDirectory,
            attemptedAtUtc: attemptedAtUtc,
            activeVersion: activeVersion,
            lastSuccessfulLoadAtUtc: lastSuccessfulLoadAtUtc,
            wouldBeVersion: wouldBeVersion,
            sourceFiles: sourceFiles,
            discovery: discovery,
            errors: [],
            fileErrors: []);
    }

    public static ProxyConfigurationValidationResult Invalid(
        string sourceDirectory,
        DateTimeOffset attemptedAtUtc,
        int? activeVersion,
        DateTimeOffset? lastSuccessfulLoadAtUtc,
        int? wouldBeVersion,
        IReadOnlyList<string> sourceFiles,
        ProxyConfigurationDiscovery discovery,
        IReadOnlyList<string> errors,
        IReadOnlyList<ProxyConfigurationFileError> fileErrors)
    {
        return new ProxyConfigurationValidationResult(
            succeeded: false,
            sourceDirectory: sourceDirectory,
            attemptedAtUtc: attemptedAtUtc,
            activeVersion: activeVersion,
            lastSuccessfulLoadAtUtc: lastSuccessfulLoadAtUtc,
            wouldBeVersion: wouldBeVersion,
            sourceFiles: sourceFiles,
            discovery: discovery,
            errors: errors,
            fileErrors: fileErrors);
    }
}
