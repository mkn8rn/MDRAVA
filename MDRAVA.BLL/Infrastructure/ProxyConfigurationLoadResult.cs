using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.BLL.Infrastructure;

public sealed record ProxyConfigurationLoadResult(
    bool Succeeded,
    string SourceDirectory,
    DateTimeOffset AttemptedAtUtc,
    IReadOnlyList<string> SourceFiles,
    ProxyConfigurationDiscovery Discovery,
    ProxyConfigurationSnapshot? Snapshot,
    IReadOnlyList<string> Errors,
    IReadOnlyList<ProxyConfigurationFileError> FileErrors,
    int? WouldBeVersion)
{
    public static ProxyConfigurationLoadResult Success(
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

    public static ProxyConfigurationLoadResult Failure(
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
