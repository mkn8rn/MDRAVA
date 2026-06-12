using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public static class ConfigLintValidationErrorMapper
{
    public static IReadOnlyList<ConfigLintFinding> ToFindings(
        IReadOnlyList<ProxyConfigurationFileError> validationErrors,
        IProxyConfigLintSourceNameFormatter sourceNameFormatter)
    {
        List<ConfigLintFinding> findings = [];
        foreach (var error in validationErrors)
        {
            findings.Add(ConfigLintFindingFactory.Error(
                "validation_error",
                error.Message,
                ConfigLintSourceNameResolver.SourceName(error.Path, sourceNameFormatter),
                null,
                "Fix the validation error before applying this config."));
        }

        return findings;
    }
}
