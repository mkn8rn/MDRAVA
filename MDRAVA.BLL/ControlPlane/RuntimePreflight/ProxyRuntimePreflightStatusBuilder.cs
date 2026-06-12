using MDRAVA.BLL.ControlPlane.Status;

namespace MDRAVA.BLL.ControlPlane.RuntimePreflight;

public static class ProxyRuntimePreflightStatusBuilder
{
    public static ProxyRuntimePreflightStatus Build(
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<ProxyRuntimePreflightCheck> checks,
        int maxReasons)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxReasons);

        var failed = checks.Any(static check => string.Equals(
            check.Severity,
            ProxyStatusText.Error,
            StringComparison.OrdinalIgnoreCase));
        var degraded = checks.Any(static check => string.Equals(
            check.Severity,
            ProxyStatusText.Warning,
            StringComparison.OrdinalIgnoreCase));
        var state = failed
            ? ProxyStatusText.Failed
            : degraded ? ProxyStatusText.Degraded : ProxyStatusText.Healthy;
        var reasons = checks
            .Where(static check => !string.Equals(
                check.Reason,
                ProxyStatusText.Ok,
                StringComparison.OrdinalIgnoreCase))
            .Select(static check => check.Reason)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maxReasons)
            .ToArray();

        return new ProxyRuntimePreflightStatus(state, generatedAtUtc, reasons, checks.ToArray());
    }
}
