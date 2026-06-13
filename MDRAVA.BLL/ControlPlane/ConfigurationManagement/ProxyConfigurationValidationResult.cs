using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public abstract record ProxyConfigurationValidationResult
{
    private ProxyConfigurationValidationResult(
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
        return new ValidResult(
            sourceDirectory,
            attemptedAtUtc,
            activeVersion,
            lastSuccessfulLoadAtUtc,
            wouldBeVersion,
            sourceFiles,
            discovery);
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
        return new InvalidResult(
            sourceDirectory,
            attemptedAtUtc,
            activeVersion,
            lastSuccessfulLoadAtUtc,
            wouldBeVersion,
            sourceFiles,
            discovery,
            errors,
            fileErrors);
    }

    public sealed record ValidResult : ProxyConfigurationValidationResult
    {
        internal ValidResult(
            string sourceDirectory,
            DateTimeOffset attemptedAtUtc,
            int? activeVersion,
            DateTimeOffset? lastSuccessfulLoadAtUtc,
            int? wouldBeVersion,
            IReadOnlyList<string> sourceFiles,
            ProxyConfigurationDiscovery discovery)
            : base(
                sourceDirectory,
                attemptedAtUtc,
                activeVersion,
                lastSuccessfulLoadAtUtc,
                wouldBeVersion,
                sourceFiles,
                discovery,
                [],
                [])
        {
        }
    }

    public sealed record InvalidResult : ProxyConfigurationValidationResult
    {
        internal InvalidResult(
            string sourceDirectory,
            DateTimeOffset attemptedAtUtc,
            int? activeVersion,
            DateTimeOffset? lastSuccessfulLoadAtUtc,
            int? wouldBeVersion,
            IReadOnlyList<string> sourceFiles,
            ProxyConfigurationDiscovery discovery,
            IReadOnlyList<string> errors,
            IReadOnlyList<ProxyConfigurationFileError> fileErrors)
            : base(
                sourceDirectory,
                attemptedAtUtc,
                activeVersion,
                lastSuccessfulLoadAtUtc,
                wouldBeVersion,
                sourceFiles,
                discovery,
                errors,
                fileErrors)
        {
        }
    }
}
