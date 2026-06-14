using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;
using MDRAVA.BLL.ControlPlane.Status;
using MDRAVA.INF.Proxy.Health;

namespace MDRAVA.INF.Runtime;

public sealed class ProxyConfigurationStore
    : IProxyConfigurationStore,
        IProxyActiveConfigurationVersionReader,
        IProxyStatusConfigurationSource
{
    private ProxyConfigurationSnapshot? _snapshot;

    public int? ActiveConfigVersion => Volatile.Read(ref _snapshot)?.Version;

    public bool HasActiveSnapshot => Volatile.Read(ref _snapshot) is not null;

    public ProxyConfigurationSnapshot Snapshot =>
        Volatile.Read(ref _snapshot)
        ?? throw new InvalidOperationException("No active proxy configuration has been loaded.");

    public ProxyConfigurationSnapshotReadResult ReadSnapshot()
    {
        var snapshot = Volatile.Read(ref _snapshot);
        return snapshot is null
            ? ProxyConfigurationSnapshotReadResult.MissingSnapshot
            : ProxyConfigurationSnapshotReadResult.Available(snapshot);
    }

    public ProxyStatusConfigurationReadResult ReadConfiguration()
    {
        var snapshotResult = ReadSnapshot();
        if (snapshotResult is not ProxyConfigurationSnapshotReadResult.AvailableResult available)
        {
            return ProxyStatusConfigurationReadResult.MissingConfiguration;
        }

        return ProxyStatusConfigurationReadResult.Available(
            ProxyStatusConfigurationSourceMapper.FromConfiguration(
                available.Snapshot,
                ProxyUpstreamHealthSourceMapper.FromRoutes(available.Snapshot.Routes)));
    }

    public ProxyConfigurationSnapshot Replace(ProxyConfigurationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Interlocked.Exchange(ref _snapshot, snapshot);
        return snapshot;
    }
}
