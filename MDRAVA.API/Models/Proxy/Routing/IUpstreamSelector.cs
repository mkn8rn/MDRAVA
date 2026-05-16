using MDRAVA.API.Proxy.Configuration.Runtime;

namespace MDRAVA.API.Proxy.Routing;

public interface IUpstreamSelector
{
    UpstreamSelection? Select(RuntimeRoute route);
}
