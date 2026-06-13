using BusinessConfigLintSummary = MDRAVA.BLL.ControlPlane.ConfigLint.ConfigLintSummary;
using BusinessConfigLintStatus = MDRAVA.BLL.ControlPlane.ConfigLint.ConfigLintStatus;

namespace MDRAVA.API.Controllers;

public sealed record ConfigLintStatusResponse(
    bool Available,
    DateTimeOffset? LastActiveLintAtUtc,
    ConfigLintSummaryResponse? LastActiveLintSummary)
{
    public static ConfigLintStatusResponse FromStatus(BusinessConfigLintStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return new ConfigLintStatusResponse(
            status.Available,
            status.LastActiveLintAtUtc,
            status.LastActiveLintSummary is null
                ? null
                : ConfigLintSummaryResponse.FromSummary(status.LastActiveLintSummary));
    }
}

public sealed record ConfigLintSummaryResponse(
    int Info,
    int Warning,
    int Error)
{
    public static ConfigLintSummaryResponse FromSummary(BusinessConfigLintSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new ConfigLintSummaryResponse(
            summary.Info,
            summary.Warning,
            summary.Error);
    }
}
