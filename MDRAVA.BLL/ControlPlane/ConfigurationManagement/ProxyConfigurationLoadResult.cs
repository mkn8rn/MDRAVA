using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public abstract record ProxyConfigurationLoadResult
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

    public sealed record LoadedResult : ProxyConfigurationLoadResult
    {
        public LoadedResult(
            string sourceDirectory,
            ProxyConfigurationSnapshot snapshot,
            ProxyConfigurationDiscovery discovery)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(discovery);

            SourceDirectory = sourceDirectory;
            Snapshot = snapshot;
            Discovery = discovery;
        }

        public override string SourceDirectory { get; }

        public override DateTimeOffset AttemptedAtUtc => Snapshot.LoadedAtUtc;

        public override IReadOnlyList<string> SourceFiles => Snapshot.SourceFiles;

        public override ProxyConfigurationDiscovery Discovery { get; }

        public ProxyConfigurationSnapshot Snapshot { get; }

        public override IReadOnlyList<string> Errors => [];

        public override IReadOnlyList<ProxyConfigurationFileError> FileErrors => [];

        public override int? WouldBeVersion => Snapshot.Version;
    }

    public sealed record ValidatedResult : ProxyConfigurationLoadResult
    {
        public ValidatedResult(
            string sourceDirectory,
            DateTimeOffset attemptedAtUtc,
            IReadOnlyList<string> sourceFiles,
            ProxyConfigurationDiscovery discovery,
            int? wouldBeVersion)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
            ArgumentNullException.ThrowIfNull(sourceFiles);
            ArgumentNullException.ThrowIfNull(discovery);

            SourceDirectory = sourceDirectory;
            AttemptedAtUtc = attemptedAtUtc;
            SourceFiles = sourceFiles;
            Discovery = discovery;
            WouldBeVersion = wouldBeVersion;
        }

        public override string SourceDirectory { get; }

        public override DateTimeOffset AttemptedAtUtc { get; }

        public override IReadOnlyList<string> SourceFiles { get; }

        public override ProxyConfigurationDiscovery Discovery { get; }

        public override IReadOnlyList<string> Errors => [];

        public override IReadOnlyList<ProxyConfigurationFileError> FileErrors => [];

        public override int? WouldBeVersion { get; }
    }

    public sealed record FailedResult : ProxyConfigurationLoadResult
    {
        public FailedResult(
            string sourceDirectory,
            DateTimeOffset attemptedAtUtc,
            IReadOnlyList<string> sourceFiles,
            ProxyConfigurationDiscovery discovery,
            IReadOnlyList<ProxyConfigurationFileError> fileErrors,
            int? wouldBeVersion)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
            ArgumentNullException.ThrowIfNull(sourceFiles);
            ArgumentNullException.ThrowIfNull(discovery);
            ArgumentNullException.ThrowIfNull(fileErrors);
            if (fileErrors.Count == 0)
            {
                throw new ArgumentException("A failed configuration load requires at least one file error.", nameof(fileErrors));
            }

            SourceDirectory = sourceDirectory;
            AttemptedAtUtc = attemptedAtUtc;
            SourceFiles = sourceFiles;
            Discovery = discovery;
            FileErrors = fileErrors;
            Errors = fileErrors
                .Select(static error => error.Path is null ? error.Message : $"{error.Path}: {error.Message}")
                .ToArray();
            WouldBeVersion = wouldBeVersion;
        }

        public override string SourceDirectory { get; }

        public override DateTimeOffset AttemptedAtUtc { get; }

        public override IReadOnlyList<string> SourceFiles { get; }

        public override ProxyConfigurationDiscovery Discovery { get; }

        public override IReadOnlyList<string> Errors { get; }

        public override IReadOnlyList<ProxyConfigurationFileError> FileErrors { get; }

        public override int? WouldBeVersion { get; }
    }
}
