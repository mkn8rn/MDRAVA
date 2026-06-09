
using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.Infrastructure;

public interface IProxyConfigurationStore
{
    bool HasActiveSnapshot { get; }

    ProxyConfigurationSnapshot Snapshot { get; }

    bool TryGetSnapshot(out ProxyConfigurationSnapshot? snapshot);

    ProxyConfigurationSnapshot Replace(ProxyConfigurationSnapshot snapshot);
}
