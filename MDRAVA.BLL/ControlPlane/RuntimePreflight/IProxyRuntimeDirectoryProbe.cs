namespace MDRAVA.BLL.ControlPlane.RuntimePreflight;

public interface IProxyRuntimeDirectoryProbe
{
    ProxyRuntimeDirectoryProbeResult Probe(string path, bool createIfMissing);
}

public abstract record ProxyRuntimeDirectoryProbeResult
{
    private ProxyRuntimeDirectoryProbeResult()
    {
    }

    public abstract bool Exists { get; }

    public abstract bool Created { get; }

    public abstract bool CanRead { get; }

    public abstract bool CanWrite { get; }

    public static ProxyRuntimeDirectoryProbeResult Missing()
    {
        return MissingResult.Instance;
    }

    public static ProxyRuntimeDirectoryProbeResult Probed(
        bool created,
        bool canRead,
        bool canWrite)
    {
        return new ProbedResult(created, canRead, canWrite);
    }

    public static ProxyRuntimeDirectoryProbeResult AccessDenied(bool exists, bool created)
    {
        return new AccessDeniedResult(exists, created);
    }

    public static ProxyRuntimeDirectoryProbeResult IoError(bool exists, bool created)
    {
        return new IoErrorResult(exists, created);
    }

    public sealed record MissingResult : ProxyRuntimeDirectoryProbeResult
    {
        public static MissingResult Instance { get; } = new();

        private MissingResult()
        {
        }

        public override bool Exists => false;

        public override bool Created => false;

        public override bool CanRead => false;

        public override bool CanWrite => false;
    }

    public sealed record ProbedResult : ProxyRuntimeDirectoryProbeResult
    {
        public ProbedResult(bool created, bool canRead, bool canWrite)
        {
            Created = created;
            CanRead = canRead;
            CanWrite = canWrite;
        }

        public override bool Exists => true;

        public override bool Created { get; }

        public override bool CanRead { get; }

        public override bool CanWrite { get; }
    }

    public sealed record AccessDeniedResult : ProxyRuntimeDirectoryProbeResult
    {
        public AccessDeniedResult(bool exists, bool created)
        {
            Exists = exists;
            Created = created;
        }

        public override bool Exists { get; }

        public override bool Created { get; }

        public override bool CanRead => false;

        public override bool CanWrite => false;
    }

    public sealed record IoErrorResult : ProxyRuntimeDirectoryProbeResult
    {
        public IoErrorResult(bool exists, bool created)
        {
            Exists = exists;
            Created = created;
        }

        public override bool Exists { get; }

        public override bool Created { get; }

        public override bool CanRead => false;

        public override bool CanWrite => false;
    }
}
