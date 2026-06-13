using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Backup;

public abstract record ProxyRestoreConfigurationValidationResult
{
    private ProxyRestoreConfigurationValidationResult()
    {
    }

    public abstract IReadOnlyList<string> Errors { get; }

    public abstract IReadOnlyList<ProxyConfigurationFileError> FileErrors { get; }

    public abstract int? WouldBeVersion { get; }

    public static ProxyRestoreConfigurationValidationResult Completed(
        IReadOnlyList<string> errors,
        IReadOnlyList<ProxyConfigurationFileError> fileErrors,
        int? wouldBeVersion)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(fileErrors);

        return errors.Count == 0 && fileErrors.Count == 0
            ? new ValidResult(wouldBeVersion)
            : new InvalidResult(errors, fileErrors, wouldBeVersion);
    }

    public sealed record ValidResult : ProxyRestoreConfigurationValidationResult
    {
        public ValidResult(int? wouldBeVersion)
        {
            WouldBeVersion = wouldBeVersion;
        }

        public override IReadOnlyList<string> Errors => [];

        public override IReadOnlyList<ProxyConfigurationFileError> FileErrors => [];

        public override int? WouldBeVersion { get; }
    }

    public sealed record InvalidResult : ProxyRestoreConfigurationValidationResult
    {
        public InvalidResult(
            IReadOnlyList<string> errors,
            IReadOnlyList<ProxyConfigurationFileError> fileErrors,
            int? wouldBeVersion)
        {
            ArgumentNullException.ThrowIfNull(errors);
            ArgumentNullException.ThrowIfNull(fileErrors);
            if (errors.Count == 0 && fileErrors.Count == 0)
            {
                throw new ArgumentException("Invalid restore configuration validation requires at least one error.");
            }

            Errors = errors;
            FileErrors = fileErrors;
            WouldBeVersion = wouldBeVersion;
        }

        public override IReadOnlyList<string> Errors { get; }

        public override IReadOnlyList<ProxyConfigurationFileError> FileErrors { get; }

        public override int? WouldBeVersion { get; }
    }
}
