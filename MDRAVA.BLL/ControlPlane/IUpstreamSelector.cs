using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane;

public interface IUpstreamSelector
{
    UpstreamSelection? Select(RuntimeRoute route);
}
