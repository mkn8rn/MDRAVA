using MDRAVA.API.Proxy.Configuration.Runtime;

namespace MDRAVA.API.Proxy.Configuration.Storage;

public sealed class ProxyConfigurationStore : IProxyConfigurationStore
{
    private ProxyConfigurationSnapshot? _snapshot;

    public bool HasActiveSnapshot => Volatile.Read(ref _snapshot) is not null;

    public ProxyConfigurationSnapshot Snapshot =>
        Volatile.Read(ref _snapshot)
        ?? throw new InvalidOperationException("No active proxy configuration has been loaded.");

    public bool TryGetSnapshot(out ProxyConfigurationSnapshot? snapshot)
    {
        snapshot = Volatile.Read(ref _snapshot);
        return snapshot is not null;
    }

    public ProxyConfigurationSnapshot Replace(ProxyConfigurationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Interlocked.Exchange(ref _snapshot, snapshot);
        return snapshot;
    }
}
