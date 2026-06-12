namespace MDRAVA.BLL.ControlPlane.RuntimePreflight;

public interface IProxyRuntimeDirectoryProbe
{
    ProxyRuntimeDirectoryProbeResult Probe(string path, bool createIfMissing);
}

public sealed record ProxyRuntimeDirectoryProbeResult(
    bool Exists,
    bool Created,
    bool CanRead,
    bool CanWrite,
    string? FailureReason)
{
    public static ProxyRuntimeDirectoryProbeResult Missing()
    {
        return new ProxyRuntimeDirectoryProbeResult(
            Exists: false,
            Created: false,
            CanRead: false,
            CanWrite: false,
            "missing");
    }

    public static ProxyRuntimeDirectoryProbeResult Probed(
        bool created,
        bool canRead,
        bool canWrite)
    {
        return new ProxyRuntimeDirectoryProbeResult(
            Exists: true,
            created,
            canRead,
            canWrite,
            canWrite ? null : "not_writable");
    }

    public static ProxyRuntimeDirectoryProbeResult NotWritable(
        bool created,
        string? failureReason = "not_writable")
    {
        return new ProxyRuntimeDirectoryProbeResult(
            Exists: true,
            created,
            CanRead: true,
            CanWrite: false,
            failureReason);
    }

    public static ProxyRuntimeDirectoryProbeResult Failed(
        bool exists,
        bool created,
        string failureReason)
    {
        return new ProxyRuntimeDirectoryProbeResult(
            exists,
            created,
            CanRead: false,
            CanWrite: false,
            failureReason);
    }
}
