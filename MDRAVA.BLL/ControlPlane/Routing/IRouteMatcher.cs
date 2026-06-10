using MDRAVA.BLL.ControlPlane.Http1;
using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Routing;

public interface IRouteMatcher
{
    RouteMatch? Match(ProxyConfigurationSnapshot snapshot, Http1RequestHead requestHead);
}
