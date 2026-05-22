namespace MDRAVA.BLL.ControlPlane;

public interface IProxyConfigLintOperations
{
    ConfigLintStatus LastActiveStatus { get; }

    ConfigLintResult LintActive();

    ConfigLintResult LintSubmitted(ConfigLintRequest request);
}

