using BusinessConfigLintFinding = MDRAVA.BLL.ControlPlane.ConfigLint.ConfigLintFinding;
using BusinessConfigLintResult = MDRAVA.BLL.ControlPlane.ConfigLint.ConfigLintResult;

namespace MDRAVA.API.Controllers;

public sealed record ConfigLintResponse(
    bool Succeeded,
    DateTimeOffset LintedAtUtc,
    ConfigLintSummaryResponse Summary,
    IReadOnlyList<ConfigLintFindingResponse> Findings,
    IReadOnlyList<ProxyConfigurationFileErrorResponse> ValidationErrors)
{
    public static ConfigLintResponse FromResult(BusinessConfigLintResult result)
    {
        return result switch
        {
            BusinessConfigLintResult.AcceptedResult accepted => FromResult(accepted, succeeded: true),
            BusinessConfigLintResult.RejectedResult rejected => FromResult(rejected, succeeded: false),
            _ => throw new InvalidOperationException($"Unknown config lint result '{result.GetType().Name}'.")
        };
    }

    private static ConfigLintResponse FromResult(
        BusinessConfigLintResult result,
        bool succeeded)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new ConfigLintResponse(
            Succeeded: succeeded,
            LintedAtUtc: result.LintedAtUtc,
            Summary: ConfigLintSummaryResponse.FromSummary(result.Summary),
            Findings: ConfigLintFindingResponse.FromFindings(result.Findings),
            ValidationErrors: ProxyConfigurationFileErrorResponse.FromErrors(result.ValidationErrors));
    }
}

public sealed record ConfigLintFindingResponse(
    string Severity,
    string Code,
    string Message,
    string? Source,
    string? Path,
    string? SuggestedFix)
{
    public static IReadOnlyList<ConfigLintFindingResponse> FromFindings(
        IReadOnlyList<BusinessConfigLintFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);

        return findings.Select(FromFinding).ToArray();
    }

    private static ConfigLintFindingResponse FromFinding(BusinessConfigLintFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);

        return new ConfigLintFindingResponse(
            finding.Severity,
            finding.Code,
            finding.Message,
            finding.Source,
            finding.Path,
            finding.SuggestedFix);
    }
}
