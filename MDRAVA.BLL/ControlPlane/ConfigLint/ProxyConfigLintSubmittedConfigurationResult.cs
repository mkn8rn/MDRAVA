using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public sealed record ProxyConfigLintSubmittedConfigurationResult
{
    private ProxyConfigLintSubmittedConfigurationResult(
        ProxyConfigLintConfigurationSnapshot? snapshot,
        IReadOnlyList<ProxyConfigurationFileError> validationErrors,
        ProxyConfigLintSubmittedConfigurationFailure? failure)
    {
        Snapshot = snapshot;
        ValidationErrors = validationErrors;
        Failure = failure;
    }

    public ProxyConfigLintConfigurationSnapshot? Snapshot { get; }

    public IReadOnlyList<ProxyConfigurationFileError> ValidationErrors { get; }

    public ProxyConfigLintSubmittedConfigurationFailure? Failure { get; }

    public static ProxyConfigLintSubmittedConfigurationResult Loaded(
        ProxyConfigLintConfigurationSnapshot snapshot,
        IReadOnlyList<ProxyConfigurationFileError> validationErrors)
    {
        return new ProxyConfigLintSubmittedConfigurationResult(
            snapshot: snapshot,
            validationErrors: validationErrors,
            failure: null);
    }

    public static ProxyConfigLintSubmittedConfigurationResult Failed(
        ProxyConfigLintSubmittedConfigurationFailureKind kind,
        string? message)
    {
        return new ProxyConfigLintSubmittedConfigurationResult(
            snapshot: null,
            validationErrors: [],
            failure: new ProxyConfigLintSubmittedConfigurationFailure(kind, message));
    }

    public static ProxyConfigLintSubmittedConfigurationResult Empty()
    {
        return new ProxyConfigLintSubmittedConfigurationResult(
            snapshot: null,
            validationErrors: [],
            failure: null);
    }
}

public sealed record ProxyConfigLintSubmittedConfigurationFailure(
    ProxyConfigLintSubmittedConfigurationFailureKind Kind,
    string? Message);

public enum ProxyConfigLintSubmittedConfigurationFailureKind
{
    JsonParseError = 0,
    YamlParseError,
    EmptySite
}
