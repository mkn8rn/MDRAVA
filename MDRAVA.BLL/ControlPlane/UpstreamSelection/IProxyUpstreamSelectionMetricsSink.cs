namespace MDRAVA.BLL.ControlPlane.UpstreamSelection;

public interface IProxyUpstreamSelectionMetricsSink
{
    void UpstreamSelected(ProxyUpstreamSelectionMetric selection);

    void NoHealthyUpstream();

    void NoAvailableUpstream();
}

public sealed record ProxyUpstreamSelectionMetric(
    string Route,
    string Upstream,
    string Scheme,
    string Protocol);
