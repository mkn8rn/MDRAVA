using BusinessProxyConfigLintFindingMetricSnapshot = MDRAVA.BLL.ControlPlane.ConfigLint.ProxyConfigLintFindingMetricSnapshot;
using BusinessProxyHttp3RequestOutcomeSnapshot = MDRAVA.BLL.ControlPlane.Metrics.ProxyHttp3RequestOutcomeSnapshot;
using BusinessProxyRequestSeriesSnapshot = MDRAVA.BLL.ControlPlane.Metrics.ProxyRequestSeriesSnapshot;
using BusinessProxyRetrySkippedSnapshot = MDRAVA.BLL.ControlPlane.Metrics.ProxyRetrySkippedSnapshot;
using BusinessProxyRouteDryRunFailureSnapshot = MDRAVA.BLL.ControlPlane.RouteDiagnostics.ProxyRouteDryRunFailureSnapshot;
using BusinessProxyUpstreamSelectionSnapshot = MDRAVA.BLL.ControlPlane.Metrics.ProxyUpstreamSelectionSnapshot;

namespace MDRAVA.API.Controllers;

public sealed record ProxyRequestSeriesSnapshotResponse(
    string Site,
    string Route,
    string Action,
    string StatusClass,
    long Count)
{
    public static IReadOnlyList<ProxyRequestSeriesSnapshotResponse> FromSnapshots(
        IReadOnlyList<BusinessProxyRequestSeriesSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        return ApiResponseList.Copy(snapshots.Select(FromSnapshot));
    }

    private static ProxyRequestSeriesSnapshotResponse FromSnapshot(BusinessProxyRequestSeriesSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new ProxyRequestSeriesSnapshotResponse(
            snapshot.Site,
            snapshot.Route,
            snapshot.Action,
            snapshot.StatusClass,
            snapshot.Count);
    }
}

public sealed record ProxyRetrySkippedSnapshotResponse(
    string Reason,
    long Count)
{
    public static IReadOnlyList<ProxyRetrySkippedSnapshotResponse> FromSnapshots(
        IReadOnlyList<BusinessProxyRetrySkippedSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        return ApiResponseList.Copy(snapshots.Select(FromSnapshot));
    }

    private static ProxyRetrySkippedSnapshotResponse FromSnapshot(BusinessProxyRetrySkippedSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new ProxyRetrySkippedSnapshotResponse(snapshot.Reason, snapshot.Count);
    }
}

public sealed record ProxyUpstreamSelectionSnapshotResponse(
    string Route,
    string Upstream,
    string Scheme,
    string Protocol,
    long Count)
{
    public static IReadOnlyList<ProxyUpstreamSelectionSnapshotResponse> FromSnapshots(
        IReadOnlyList<BusinessProxyUpstreamSelectionSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        return ApiResponseList.Copy(snapshots.Select(FromSnapshot));
    }

    private static ProxyUpstreamSelectionSnapshotResponse FromSnapshot(
        BusinessProxyUpstreamSelectionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new ProxyUpstreamSelectionSnapshotResponse(
            snapshot.Route,
            snapshot.Upstream,
            snapshot.Scheme,
            snapshot.Protocol,
            snapshot.Count);
    }
}

public sealed record ProxyHttp3RequestOutcomeSnapshotResponse(
    string Method,
    string Outcome,
    string StatusClass,
    long Count)
{
    public static IReadOnlyList<ProxyHttp3RequestOutcomeSnapshotResponse> FromSnapshots(
        IReadOnlyList<BusinessProxyHttp3RequestOutcomeSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        return ApiResponseList.Copy(snapshots.Select(FromSnapshot));
    }

    private static ProxyHttp3RequestOutcomeSnapshotResponse FromSnapshot(
        BusinessProxyHttp3RequestOutcomeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new ProxyHttp3RequestOutcomeSnapshotResponse(
            snapshot.Method,
            snapshot.Outcome,
            snapshot.StatusClass,
            snapshot.Count);
    }
}

public sealed record ProxyConfigLintFindingMetricSnapshotResponse(
    string Severity,
    string Code,
    long Count)
{
    public static IReadOnlyList<ProxyConfigLintFindingMetricSnapshotResponse> FromSnapshots(
        IReadOnlyList<BusinessProxyConfigLintFindingMetricSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        return ApiResponseList.Copy(snapshots.Select(FromSnapshot));
    }

    private static ProxyConfigLintFindingMetricSnapshotResponse FromSnapshot(
        BusinessProxyConfigLintFindingMetricSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new ProxyConfigLintFindingMetricSnapshotResponse(
            snapshot.Severity,
            snapshot.Code,
            snapshot.Count);
    }
}

public sealed record ProxyRouteDryRunFailureSnapshotResponse(
    string Reason,
    long Count)
{
    public static IReadOnlyList<ProxyRouteDryRunFailureSnapshotResponse> FromSnapshots(
        IReadOnlyList<BusinessProxyRouteDryRunFailureSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        return ApiResponseList.Copy(snapshots.Select(FromSnapshot));
    }

    private static ProxyRouteDryRunFailureSnapshotResponse FromSnapshot(
        BusinessProxyRouteDryRunFailureSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new ProxyRouteDryRunFailureSnapshotResponse(snapshot.Reason, snapshot.Count);
    }
}
