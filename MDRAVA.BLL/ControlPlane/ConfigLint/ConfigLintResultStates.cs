using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public abstract partial record ConfigLintResult
{
    public sealed record AcceptedResult : ConfigLintResult
    {
        internal AcceptedResult(
            DateTimeOffset lintedAtUtc,
            ConfigLintSummary summary,
            IReadOnlyList<ConfigLintFinding> findings,
            IReadOnlyList<ProxyConfigurationFileError> validationErrors)
            : base(lintedAtUtc, summary, findings, validationErrors)
        {
        }
    }

    public sealed record RejectedResult : ConfigLintResult
    {
        internal RejectedResult(
            DateTimeOffset lintedAtUtc,
            ConfigLintSummary summary,
            IReadOnlyList<ConfigLintFinding> findings,
            IReadOnlyList<ProxyConfigurationFileError> validationErrors)
            : base(lintedAtUtc, summary, findings, validationErrors)
        {
        }
    }
}
