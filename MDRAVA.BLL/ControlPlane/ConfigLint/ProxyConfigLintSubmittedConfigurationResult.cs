using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public abstract partial record ProxyConfigLintSubmittedConfigurationResult
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

}
