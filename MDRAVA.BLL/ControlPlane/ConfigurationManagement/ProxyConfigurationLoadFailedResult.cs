using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public abstract partial record ProxyConfigurationLoadResult
{
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

            var fileErrorSnapshot = ConfigurationManagementList.Copy(fileErrors);

            SourceDirectory = sourceDirectory;
            AttemptedAtUtc = attemptedAtUtc;
            SourceFiles = ConfigurationManagementList.Copy(sourceFiles);
            Discovery = discovery;
            FileErrors = fileErrorSnapshot;
            Errors = ConfigurationManagementList.Copy(fileErrorSnapshot
                .Select(static error => error.Path is null ? error.Message : $"{error.Path}: {error.Message}")
                .ToArray());
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
