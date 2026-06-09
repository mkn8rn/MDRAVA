using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane;

public interface IRouteMatcher
{
    RouteMatch? Match(ProxyConfigurationSnapshot snapshot, Http1RequestHead requestHead);
}
