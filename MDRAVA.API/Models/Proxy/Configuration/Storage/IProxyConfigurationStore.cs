
namespace MDRAVA.API.Proxy.Configuration.Storage;

public interface IProxyConfigurationStore
{
    bool HasActiveSnapshot { get; }

    ProxyConfigurationSnapshot Snapshot { get; }

    bool TryGetSnapshot(out ProxyConfigurationSnapshot? snapshot);

    ProxyConfigurationSnapshot Replace(ProxyConfigurationSnapshot snapshot);
}
