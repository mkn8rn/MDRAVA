using MDRAVA.BLL.ControlPlane.Status;

namespace MDRAVA.BLL.ControlPlane.RuntimePreflight;

public static class ProxyRuntimePreflightStatusBuilder
{
    public static ProxyRuntimePreflightStatus Build(
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<ProxyRuntimePreflightCheck> checks,
        int maxReasons)
    {
        return ProxyRuntimePreflightStatus.Completed(generatedAtUtc, checks, maxReasons);
    }
}
