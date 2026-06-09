using MDRAVA.API.Proxy.Protocol;

namespace MDRAVA.API.Proxy.Routing;

public interface IRouteMatcher
{
    RouteMatch? Match(ProxyConfigurationSnapshot snapshot, Http1RequestHead requestHead);
}
