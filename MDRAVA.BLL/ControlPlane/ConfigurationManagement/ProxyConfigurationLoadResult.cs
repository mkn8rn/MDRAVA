using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public abstract partial record ProxyConfigurationLoadResult
{
    private ProxyConfigurationLoadResult()
    {
    }

    public abstract string SourceDirectory { get; }

    public abstract DateTimeOffset AttemptedAtUtc { get; }

    public abstract IReadOnlyList<string> SourceFiles { get; }

    public abstract ProxyConfigurationDiscovery Discovery { get; }

    public abstract IReadOnlyList<string> Errors { get; }

    public abstract IReadOnlyList<ProxyConfigurationFileError> FileErrors { get; }

    public abstract int? WouldBeVersion { get; }

    public static ProxyConfigurationLoadResult Loaded(
        string sourceDirectory,
        ProxyConfigurationSnapshot snapshot,
        ProxyConfigurationDiscovery discovery)
    {
        return new LoadedResult(sourceDirectory, snapshot, discovery);
    }

    public static ProxyConfigurationLoadResult Validated(
        string sourceDirectory,
        DateTimeOffset attemptedAtUtc,
        IReadOnlyList<string> sourceFiles,
        ProxyConfigurationDiscovery discovery,
        int? wouldBeVersion)
    {
        return new ValidatedResult(
            sourceDirectory,
            attemptedAtUtc,
            sourceFiles,
            discovery,
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
        return new FailedResult(
            sourceDirectory,
            attemptedAtUtc,
            sourceFiles,
            discovery,
            fileErrors,
            wouldBeVersion);
    }

}
