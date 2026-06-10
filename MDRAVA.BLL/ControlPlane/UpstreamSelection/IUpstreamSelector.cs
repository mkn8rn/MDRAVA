using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.UpstreamSelection;

public interface IUpstreamSelector
{
    UpstreamSelection? Select(RuntimeRoute route);
}
