using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;
using MDRAVA.BLL.ControlPlane.Status;

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

    public bool TryGetSnapshot(out ProxyConfigurationSnapshot? snapshot)
    {
        snapshot = Volatile.Read(ref _snapshot);
        return snapshot is not null;
    }

    public ProxyStatusConfigurationReadResult ReadConfiguration()
    {
        if (!TryGetSnapshot(out var snapshot) || snapshot is null)
        {
            return ProxyStatusConfigurationReadResult.MissingConfiguration;
        }

        return ProxyStatusConfigurationReadResult.Available(
            ProxyStatusConfigurationSourceMapper.FromConfiguration(snapshot));
    }

    public ProxyConfigurationSnapshot Replace(ProxyConfigurationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Interlocked.Exchange(ref _snapshot, snapshot);
        return snapshot;
    }
}
