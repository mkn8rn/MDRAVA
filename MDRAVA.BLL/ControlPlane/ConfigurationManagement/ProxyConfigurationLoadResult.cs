using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public sealed record ProxyConfigurationLoadResult
{
    private ProxyConfigurationLoadResult(
        bool succeeded,
        string sourceDirectory,
        DateTimeOffset attemptedAtUtc,
        IReadOnlyList<string> sourceFiles,
        ProxyConfigurationDiscovery discovery,
        ProxyConfigurationSnapshot? snapshot,
        IReadOnlyList<string> errors,
        IReadOnlyList<ProxyConfigurationFileError> fileErrors,
        int? wouldBeVersion)
    {
        Succeeded = succeeded;
        SourceDirectory = sourceDirectory;
        AttemptedAtUtc = attemptedAtUtc;
        SourceFiles = sourceFiles;
        Discovery = discovery;
        Snapshot = snapshot;
        Errors = errors;
        FileErrors = fileErrors;
        WouldBeVersion = wouldBeVersion;
    }

    public bool Succeeded { get; }

    public string SourceDirectory { get; }

    public DateTimeOffset AttemptedAtUtc { get; }

    public IReadOnlyList<string> SourceFiles { get; }

    public ProxyConfigurationDiscovery Discovery { get; }

    public ProxyConfigurationSnapshot? Snapshot { get; }

    public IReadOnlyList<string> Errors { get; }

    public IReadOnlyList<ProxyConfigurationFileError> FileErrors { get; }

    public int? WouldBeVersion { get; }

    public static ProxyConfigurationLoadResult Loaded(
        string sourceDirectory,
        ProxyConfigurationSnapshot snapshot,
        ProxyConfigurationDiscovery discovery)
    {
        return new ProxyConfigurationLoadResult(
            true,
            sourceDirectory,
            snapshot.LoadedAtUtc,
            snapshot.SourceFiles,
            discovery,
            snapshot,
            [],
            [],
            snapshot.Version);
    }

    public static ProxyConfigurationLoadResult Validated(
        string sourceDirectory,
        DateTimeOffset attemptedAtUtc,
        IReadOnlyList<string> sourceFiles,
        ProxyConfigurationDiscovery discovery,
        int? wouldBeVersion)
    {
        return new ProxyConfigurationLoadResult(
            true,
            sourceDirectory,
            attemptedAtUtc,
            sourceFiles,
            discovery,
            null,
            [],
            [],
            wouldBeVersion);
    }

    public static ProxyConfigurationLoadResult Failed(
        string sourceDirectory,
        DateTimeOffset attemptedAtUtc,
        IReadOnlyList<string> sourceFiles,
        ProxyConfigurationDiscovery discovery,
        IReadOnlyList<ProxyConfigurationFileError> fileErrors,
        int? wouldBeVersion)
    {
        return new ProxyConfigurationLoadResult(
            false,
            sourceDirectory,
            attemptedAtUtc,
            sourceFiles,
            discovery,
            null,
            fileErrors.Select(static error => error.Path is null ? error.Message : $"{error.Path}: {error.Message}").ToArray(),
            fileErrors,
            wouldBeVersion);
    }
}
