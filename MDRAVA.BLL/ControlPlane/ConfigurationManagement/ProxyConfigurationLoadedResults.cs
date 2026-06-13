using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public abstract partial record ProxyConfigurationLoadResult
{
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
}
