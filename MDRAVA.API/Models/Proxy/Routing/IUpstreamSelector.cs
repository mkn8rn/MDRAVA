
namespace MDRAVA.API.Proxy.Routing;

public interface IUpstreamSelector
{
    UpstreamSelection? Select(RuntimeRoute route);
}
