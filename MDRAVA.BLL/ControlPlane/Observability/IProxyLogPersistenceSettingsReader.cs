using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Observability;

public interface IProxyLogPersistenceSettingsReader
{
    bool TryGetLogPersistenceOptions(out ProxyLogPersistenceOptions options);
}
