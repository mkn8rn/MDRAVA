using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public abstract partial record ProxyConfigLintSubmittedConfigurationResult
{
    public sealed record LoadedResult : ProxyConfigLintSubmittedConfigurationResult
    {
        public LoadedResult(
            ProxyConfigLintConfigurationSnapshot snapshot,
            IReadOnlyList<ProxyConfigurationFileError> validationErrors)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(validationErrors);

            Snapshot = snapshot;
            ValidationErrors = validationErrors.ToArray();
        }

        public ProxyConfigLintConfigurationSnapshot Snapshot { get; }

        public IReadOnlyList<ProxyConfigurationFileError> ValidationErrors { get; }
    }

    public sealed record FailedResult : ProxyConfigLintSubmittedConfigurationResult
    {
        public FailedResult(ProxyConfigLintSubmittedConfigurationFailure failure)
        {
            ArgumentNullException.ThrowIfNull(failure);

            Failure = failure;
        }

        public ProxyConfigLintSubmittedConfigurationFailure Failure { get; }
    }

    public sealed record EmptyResult : ProxyConfigLintSubmittedConfigurationResult
    {
        public static EmptyResult Instance { get; } = new();

        private EmptyResult()
        {
        }
    }
}
