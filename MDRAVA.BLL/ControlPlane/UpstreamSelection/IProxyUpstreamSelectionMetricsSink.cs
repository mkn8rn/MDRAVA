using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.UpstreamSelection;

public interface IProxyUpstreamSelectionMetricsSink
{
    void UpstreamSelected(RuntimeUpstream upstream);

    void NoHealthyUpstream();

    void NoAvailableUpstream();
}
