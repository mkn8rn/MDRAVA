using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.INF.Configuration;

public sealed class ProxyConfigurationReadProjectionSource
    : IProxyConfigurationReadProjectionSource<ProxyConfigurationProjection>
{
    private readonly IProxyConfigurationStore _configurationStore;
    private readonly IRuntimeHttp3PlatformSupportSource _http3PlatformSupportSource;

    public ProxyConfigurationReadProjectionSource(
        IProxyConfigurationStore configurationStore,
        IRuntimeHttp3PlatformSupportSource http3PlatformSupportSource)
    {
        _configurationStore = configurationStore;
        _http3PlatformSupportSource = http3PlatformSupportSource;
    }

    public ProxyConfigurationReadProjectionResult<ProxyConfigurationProjection> ReadCurrent()
    {
        return _configurationStore.ReadSnapshot() is ProxyConfigurationSnapshotReadResult.AvailableResult available
            ? ProxyConfigurationReadProjectionResult<ProxyConfigurationProjection>.Available(
                ProxyConfigurationProjectionMapper.ToProjection(available.Snapshot, _http3PlatformSupportSource.Read()))
            : ProxyConfigurationReadProjectionResult<ProxyConfigurationProjection>.MissingConfiguration;
    }
}
