using BusinessProxyAcmeSubsystemSummary = MDRAVA.BLL.ControlPlane.Status.ProxyAcmeSubsystemSummary;
using BusinessProxyCertificateSubsystemSummary = MDRAVA.BLL.ControlPlane.Status.ProxyCertificateSubsystemSummary;
using BusinessProxySubsystemIssueSummary = MDRAVA.BLL.ControlPlane.Status.ProxySubsystemIssueSummary;

namespace MDRAVA.API.Controllers;

public sealed record ProxySubsystemIssueSummaryResponse(
    DateTimeOffset TimestampUtc,
    string Category,
    string Reason,
    string? AffectedIdentity)
{
    public static ProxySubsystemIssueSummaryResponse FromSummary(BusinessProxySubsystemIssueSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new ProxySubsystemIssueSummaryResponse(
            summary.TimestampUtc,
            summary.Category,
            summary.Reason,
            summary.AffectedIdentity);
    }
}

public sealed record ProxyCertificateSubsystemSummaryResponse(
    int Configured,
    int Loaded,
    int MissingReferences,
    int Expired,
    int NotYetValid,
    int ExpiringSoon,
    ProxySubsystemIssueSummaryResponse? LastIssue)
{
    public static ProxyCertificateSubsystemSummaryResponse FromSummary(BusinessProxyCertificateSubsystemSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new ProxyCertificateSubsystemSummaryResponse(
            summary.Configured,
            summary.Loaded,
            summary.MissingReferences,
            summary.Expired,
            summary.NotYetValid,
            summary.ExpiringSoon,
            summary.LastIssue is null ? null : ProxySubsystemIssueSummaryResponse.FromSummary(summary.LastIssue));
    }
}

public sealed record ProxyAcmeSubsystemSummaryResponse(
    bool Enabled,
    int Configured,
    int Active,
    int Failed,
    int RenewalBackoff,
    ProxySubsystemIssueSummaryResponse? LastIssue)
{
    public static ProxyAcmeSubsystemSummaryResponse FromSummary(BusinessProxyAcmeSubsystemSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new ProxyAcmeSubsystemSummaryResponse(
            summary.Enabled,
            summary.Configured,
            summary.Active,
            summary.Failed,
            summary.RenewalBackoff,
            summary.LastIssue is null ? null : ProxySubsystemIssueSummaryResponse.FromSummary(summary.LastIssue));
    }
}
