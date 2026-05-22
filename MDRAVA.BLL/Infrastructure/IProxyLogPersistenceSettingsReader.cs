using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.Infrastructure;

public interface IProxyLogPersistenceSettingsReader
{
    bool TryGetLogPersistenceOptions(out ProxyLogPersistenceOptions options);
}
