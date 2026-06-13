using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public abstract record ProxyConfigLintSubmittedConfigurationResult
{
    private ProxyConfigLintSubmittedConfigurationResult()
    {
    }

    public static ProxyConfigLintSubmittedConfigurationResult Loaded(
        ProxyConfigLintConfigurationSnapshot snapshot,
        IReadOnlyList<ProxyConfigurationFileError> validationErrors)
    {
        return new LoadedResult(snapshot, validationErrors);
    }

    public static ProxyConfigLintSubmittedConfigurationResult Failed(
        ProxyConfigLintSubmittedConfigurationFailureKind kind,
        string message)
    {
        return new FailedResult(new ProxyConfigLintSubmittedConfigurationFailure(kind, message));
    }

    public static ProxyConfigLintSubmittedConfigurationResult Empty()
    {
        return EmptyResult.Instance;
    }

    public sealed record LoadedResult : ProxyConfigLintSubmittedConfigurationResult
    {
        public LoadedResult(
            ProxyConfigLintConfigurationSnapshot snapshot,
            IReadOnlyList<ProxyConfigurationFileError> validationErrors)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(validationErrors);

            Snapshot = snapshot;
            ValidationErrors = validationErrors;
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

public sealed record ProxyConfigLintSubmittedConfigurationFailure
{
    public ProxyConfigLintSubmittedConfigurationFailure(
        ProxyConfigLintSubmittedConfigurationFailureKind kind,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Kind = kind;
        Message = message;
    }

    public ProxyConfigLintSubmittedConfigurationFailureKind Kind { get; }

    public string Message { get; }
}

public enum ProxyConfigLintSubmittedConfigurationFailureKind
{
    JsonParseError = 0,
    YamlParseError,
    EmptySite
}
