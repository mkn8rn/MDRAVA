using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.ConfigLint;

namespace MDRAVA.API.Controllers;

public sealed record ConfigLintResponse(
    bool Succeeded,
    DateTimeOffset LintedAtUtc,
    ConfigLintSummary Summary,
    IReadOnlyList<ConfigLintFinding> Findings,
    IReadOnlyList<ProxyConfigurationFileError> ValidationErrors)
{
    public static ConfigLintResponse FromResult(ConfigLintResult result)
    {
        return result switch
        {
            ConfigLintResult.AcceptedResult accepted => FromResult(accepted, succeeded: true),
            ConfigLintResult.RejectedResult rejected => FromResult(rejected, succeeded: false),
            _ => throw new InvalidOperationException($"Unknown config lint result '{result.GetType().Name}'.")
        };
    }

    private static ConfigLintResponse FromResult(
        ConfigLintResult result,
        bool succeeded)
    {
        return new ConfigLintResponse(
            Succeeded: succeeded,
            LintedAtUtc: result.LintedAtUtc,
            Summary: result.Summary,
            Findings: result.Findings,
            ValidationErrors: result.ValidationErrors);
    }
}
