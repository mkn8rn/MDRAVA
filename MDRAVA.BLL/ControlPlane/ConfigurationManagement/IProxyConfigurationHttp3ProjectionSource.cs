using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Http3;

namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public interface IProxyConfigurationHttp3ProjectionSource
{
    RuntimeHttp3SupportProjection Project(ProxyConfigurationSnapshot snapshot);
}
