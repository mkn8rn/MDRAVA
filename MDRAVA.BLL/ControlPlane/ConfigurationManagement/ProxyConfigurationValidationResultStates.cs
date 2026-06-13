using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public abstract partial record ProxyConfigurationValidationResult
{
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
