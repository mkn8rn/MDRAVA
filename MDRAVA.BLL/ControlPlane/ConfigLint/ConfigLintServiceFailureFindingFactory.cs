using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public static class ConfigLintServiceFailureFindingFactory
{
    public static ConfigLintFinding NoActiveConfig()
    {
        return ConfigLintFindingFactory.Error(
            "no_active_config",
            "No active proxy configuration is loaded.",
            null,
            null,
            "Load a valid config before linting the active configuration source.");
    }

    public static ConfigLintFinding EmptySubmittedConfig()
    {
        return ConfigLintFindingFactory.Error(
            "empty_config",
            "Submitted config did not contain a site object.",
            SiteConfigurationSource.LintInputPath,
            null,
            "Submit one site configuration object.");
    }
}
