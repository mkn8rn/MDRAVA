using MDRAVA.BLL.ControlPlane.Status;

namespace MDRAVA.BLL.ControlPlane.RuntimePreflight;

public static class ProxyRuntimePreflightCheckFactory
{
    public static ProxyRuntimePreflightCheck UnsafePath(
        ProxyRuntimePreflightDirectoryRequirement requirement)
    {
        return new ProxyRuntimePreflightCheck(
            requirement.Name,
            requirement.RelativePath,
            Exists: false,
            Created: false,
            CanRead: false,
            CanWrite: false,
            requirement.Critical ? ProxyStatusText.Error : ProxyStatusText.Warning,
            "unsafe_path");
    }

    public static ProxyRuntimePreflightCheck FromProbeResult(
        ProxyRuntimePreflightDirectoryRequirement requirement,
        ProxyRuntimeDirectoryProbeResult result)
    {
        var classification = ProxyRuntimePreflightProbePolicy.Classify(result, requirement.Critical);
        return new ProxyRuntimePreflightCheck(
            requirement.Name,
            requirement.RelativePath,
            result.Exists,
            result.Created,
            result.CanRead,
            result.CanWrite,
            classification.Severity,
            classification.Reason);
    }
}
