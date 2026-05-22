namespace MDRAVA.BLL.Infrastructure;

public interface IProxyRuntimeDirectoryProbe
{
    ProxyRuntimeDirectoryProbeResult Probe(string path, bool createIfMissing);
}

public sealed record ProxyRuntimeDirectoryProbeResult(
    bool Exists,
    bool Created,
    bool CanRead,
    bool CanWrite,
    string? FailureReason);
