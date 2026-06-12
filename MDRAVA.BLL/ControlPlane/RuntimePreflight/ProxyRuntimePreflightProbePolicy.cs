using MDRAVA.BLL.ControlPlane.Status;

namespace MDRAVA.BLL.ControlPlane.RuntimePreflight;

public sealed record ProxyRuntimePreflightProbeClassification(
    string Severity,
    string Reason);

public static class ProxyRuntimePreflightProbePolicy
{
    public static ProxyRuntimePreflightProbeClassification Classify(
        ProxyRuntimeDirectoryProbeResult result,
        bool critical)
    {
        var reason = Reason(result);
        var severity = string.Equals(reason, ProxyStatusText.Ok, StringComparison.OrdinalIgnoreCase)
            ? ProxyStatusText.Info
            : critical ? ProxyStatusText.Error : ProxyStatusText.Warning;

        return new ProxyRuntimePreflightProbeClassification(severity, reason);
    }

    private static string Reason(ProxyRuntimeDirectoryProbeResult result)
    {
        if (string.Equals(result.FailureReason, "access_denied", StringComparison.OrdinalIgnoreCase))
        {
            return "directory_access_denied";
        }

        if (string.Equals(result.FailureReason, "io_error", StringComparison.OrdinalIgnoreCase))
        {
            return "directory_io_error";
        }

        if (!result.Exists)
        {
            return "missing_directory";
        }

        if (!result.CanRead)
        {
            return "directory_not_readable";
        }

        if (!result.CanWrite)
        {
            return "directory_not_writable";
        }

        return ProxyStatusText.Ok;
    }
}
