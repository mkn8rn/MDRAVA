using BusinessConfigLintFinding = MDRAVA.BLL.ControlPlane.ConfigLint.ConfigLintFinding;
using BusinessConfigLintResult = MDRAVA.BLL.ControlPlane.ConfigLint.ConfigLintResult;

namespace MDRAVA.API.Controllers;

public sealed record ConfigLintResponse
{
    public ConfigLintResponse(
        bool succeeded,
        DateTimeOffset lintedAtUtc,
        ConfigLintSummaryResponse summary,
        IReadOnlyList<ConfigLintFindingResponse> findings,
        IReadOnlyList<ProxyConfigurationFileErrorResponse> validationErrors)
    {
        ArgumentNullException.ThrowIfNull(summary);

        Succeeded = succeeded;
        LintedAtUtc = lintedAtUtc;
        Summary = summary;
        Findings = ApiResponseList.Copy(findings);
        ValidationErrors = ApiResponseList.Copy(validationErrors);
    }

    public bool Succeeded { get; }

    public DateTimeOffset LintedAtUtc { get; }

    public ConfigLintSummaryResponse Summary { get; }

    public IReadOnlyList<ConfigLintFindingResponse> Findings { get; }

    public IReadOnlyList<ProxyConfigurationFileErrorResponse> ValidationErrors { get; }

    public static ConfigLintResponse FromResult(BusinessConfigLintResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

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
            succeeded: succeeded,
            lintedAtUtc: result.LintedAtUtc,
            summary: ConfigLintSummaryResponse.FromSummary(result.Summary),
            findings: ConfigLintFindingResponse.FromFindings(result.Findings),
            validationErrors: ProxyConfigurationFileErrorResponse.FromErrors(result.ValidationErrors));
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

        return ApiResponseList.Copy(findings.Select(FromFinding));
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
