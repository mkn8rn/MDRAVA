using MDRAVA.BLL.ControlPlane.Status;

namespace MDRAVA.BLL.ControlPlane.RuntimePreflight;

public sealed record ProxyRuntimePreflightProbeClassification
{
    public ProxyRuntimePreflightProbeClassification(
        string Severity,
        string Reason)
    {
        ProxyStatusFacts.RequireText(Severity, nameof(Severity));
        ProxyStatusFacts.RequireText(Reason, nameof(Reason));

        this.Severity = Severity;
        this.Reason = Reason;
    }

    public string Severity { get; }

    public string Reason { get; }
}

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
        if (result is ProxyRuntimeDirectoryProbeResult.AccessDeniedResult)
        {
            return "directory_access_denied";
        }

        if (result is ProxyRuntimeDirectoryProbeResult.IoErrorResult)
        {
            return "directory_io_error";
        }

        if (result is ProxyRuntimeDirectoryProbeResult.MissingResult)
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
