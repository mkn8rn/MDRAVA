namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public sealed record ProxyConfigLintSubmittedConfigurationResult(
    ProxyConfigLintConfigurationSnapshot? Snapshot,
    IReadOnlyList<ProxyConfigurationFileError> ValidationErrors,
    ProxyConfigLintSubmittedConfigurationFailure? Failure);

public sealed record ProxyConfigLintSubmittedConfigurationFailure(
    ProxyConfigLintSubmittedConfigurationFailureKind Kind,
    string? Message);

public enum ProxyConfigLintSubmittedConfigurationFailureKind
{
    JsonParseError = 0,
    YamlParseError,
    EmptySite
}
