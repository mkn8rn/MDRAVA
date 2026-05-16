using MDRAVA.API.Proxy.Configuration.Runtime;

namespace MDRAVA.API.Models.Configuration.Loading;

public sealed record ProxyConfigurationLoadResult(
    bool Succeeded,
    string SourceDirectory,
    DateTimeOffset AttemptedAtUtc,
    IReadOnlyList<string> SourceFiles,
    ProxyConfigurationSnapshot? Snapshot,
    IReadOnlyList<string> Errors,
    IReadOnlyList<ProxyConfigurationFileError> FileErrors,
    int? WouldBeVersion)
{
    public static ProxyConfigurationLoadResult Success(string sourceDirectory, ProxyConfigurationSnapshot snapshot)
    {
        return new ProxyConfigurationLoadResult(
            true,
            sourceDirectory,
            snapshot.LoadedAtUtc,
            snapshot.SourceFiles,
            snapshot,
            [],
            [],
            snapshot.Version);
    }

    public static ProxyConfigurationLoadResult Failure(
        string sourceDirectory,
        DateTimeOffset attemptedAtUtc,
        IReadOnlyList<string> sourceFiles,
        IReadOnlyList<ProxyConfigurationFileError> fileErrors,
        int? wouldBeVersion)
    {
        return new ProxyConfigurationLoadResult(
            false,
            sourceDirectory,
            attemptedAtUtc,
            sourceFiles,
            null,
            fileErrors.Select(static error => error.Path is null ? error.Message : $"{error.Path}: {error.Message}").ToArray(),
            fileErrors,
            wouldBeVersion);
    }
}
