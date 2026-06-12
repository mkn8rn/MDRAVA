namespace MDRAVA.BLL.ControlPlane.RuntimePreflight;

public interface IProxyRuntimeDirectoryProbe
{
    ProxyRuntimeDirectoryProbeResult Probe(string path, bool createIfMissing);
}

public sealed record ProxyRuntimeDirectoryProbeResult
{
    private ProxyRuntimeDirectoryProbeResult(
        bool exists,
        bool created,
        bool canRead,
        bool canWrite,
        string? failureReason)
    {
        Exists = exists;
        Created = created;
        CanRead = canRead;
        CanWrite = canWrite;
        FailureReason = failureReason;
    }

    public bool Exists { get; }

    public bool Created { get; }

    public bool CanRead { get; }

    public bool CanWrite { get; }

    public string? FailureReason { get; }

    public static ProxyRuntimeDirectoryProbeResult Missing()
    {
        return new ProxyRuntimeDirectoryProbeResult(
            exists: false,
            created: false,
            canRead: false,
            canWrite: false,
            failureReason: "missing");
    }

    public static ProxyRuntimeDirectoryProbeResult Probed(
        bool created,
        bool canRead,
        bool canWrite)
    {
        return new ProxyRuntimeDirectoryProbeResult(
            exists: true,
            created: created,
            canRead: canRead,
            canWrite: canWrite,
            failureReason: canWrite ? null : "not_writable");
    }

    public static ProxyRuntimeDirectoryProbeResult NotWritable(
        bool created,
        string? failureReason = "not_writable")
    {
        return new ProxyRuntimeDirectoryProbeResult(
            exists: true,
            created: created,
            canRead: true,
            canWrite: false,
            failureReason: failureReason);
    }

    public static ProxyRuntimeDirectoryProbeResult Failed(
        bool exists,
        bool created,
        string failureReason)
    {
        return new ProxyRuntimeDirectoryProbeResult(
            exists: exists,
            created: created,
            canRead: false,
            canWrite: false,
            failureReason: failureReason);
    }
}
