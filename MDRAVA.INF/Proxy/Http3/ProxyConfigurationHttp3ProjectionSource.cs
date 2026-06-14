using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;
using MDRAVA.BLL.ControlPlane.Http3;

namespace MDRAVA.INF.Proxy.Http3;

public sealed class ProxyConfigurationHttp3ProjectionSource : IProxyConfigurationHttp3ProjectionSource
{
    private readonly IRuntimeHttp3PlatformSupportSource _platformSupportSource;

    public ProxyConfigurationHttp3ProjectionSource(
        IRuntimeHttp3PlatformSupportSource platformSupportSource)
    {
        _platformSupportSource = platformSupportSource;
    }

    public RuntimeHttp3SupportProjection Project(ProxyConfigurationSnapshot snapshot)
    {
        return Http3RuntimeSupport.ProjectConfiguration(
            ProxyHttp3SupportConfigurationSourceMapper.FromConfiguration(
                snapshot.Listeners,
                snapshot.Routes),
            _platformSupportSource.Read());
    }
}

public static class ProxyHttp3SupportConfigurationSourceMapper
{
    public static Http3SupportConfigurationSource FromConfiguration(
        IReadOnlyList<RuntimeListener> listeners,
        IReadOnlyList<RuntimeRoute> routes)
    {
        return new Http3SupportConfigurationSource(
            listeners
                .Select(static listener => new Http3SupportListenerSource(
                    listener.Http3.Configured,
                    listener.Http3.EnabledForTraffic,
                    listener.Http3.EnablementLevel,
                    Http3AltSvcListenerPolicy.IsEnabled(new Http3AltSvcListenerInput(
                        listener.Http3.EnabledForTraffic,
                        listener.Http3.EnablementLevel,
                        listener.Http3AltSvc.Enabled,
                        listener.Http3AltSvc.MaxAgeSeconds,
                        listener.Port,
                        listener.QuicIdentity?.Key)),
                    listener.Http3AltSvc.MaxAgeSeconds,
                    listener.QuicIdentity?.Key))
                .ToArray(),
            routes.Any(static route => route.Upstreams.Any(static upstream =>
                RuntimeUpstreamProtocol.IsHttp3(upstream.Protocol))));
    }
}
