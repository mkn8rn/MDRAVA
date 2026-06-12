namespace MDRAVA.BLL.ControlPlane.UpstreamSelection;

public interface IUpstreamSelector
{
    UpstreamSelection? Select(UpstreamSelectionRoute route);
}
