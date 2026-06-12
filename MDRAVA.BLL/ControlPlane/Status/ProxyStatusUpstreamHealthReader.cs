using MDRAVA.BLL.ControlPlane.HealthChecks;

namespace MDRAVA.BLL.ControlPlane.Status;

public sealed class ProxyStatusUpstreamHealthReader : IProxyStatusUpstreamHealthReader
{
    private readonly IProxyStatusConfigurationSource _configurationSource;
    private readonly IProxyStatusUpstreamHealthSource _upstreamHealthSource;

    public ProxyStatusUpstreamHealthReader(
        IProxyStatusConfigurationSource configurationSource,
        IProxyStatusUpstreamHealthSource upstreamHealthSource)
    {
        _configurationSource = configurationSource;
        _upstreamHealthSource = upstreamHealthSource;
    }

    public IReadOnlyList<ProxyUpstreamStatusResponse> ReadUpstreams()
    {
        var configuration = _configurationSource.TryReadSnapshot(out var snapshot) ? snapshot : null;
        return _upstreamHealthSource.ReadUpstreams(
            ProxyUpstreamHealthSourceMapper.FromSnapshot(configuration));
    }
}
