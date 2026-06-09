using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane;

public static class ProxyListenerHttp3StatusMapper
{
    public static ProxyListenerHttp3Status ToStatus(this RuntimeHttp3ListenerReadiness readiness)
    {
        return new ProxyListenerHttp3Status(
            readiness.Configured,
            readiness.DefaultEnabled,
            readiness.EnablementLevel,
            readiness.EnabledForTraffic,
            readiness.DisabledReason,
            readiness.AltSvcConfigured,
            readiness.AltSvcMaxAgeSeconds,
            readiness.UdpQuicListenerIdentityModeled,
            readiness.QuicIdentity is null
                ? null
                : new ProxyQuicListenerIdentity(
                    readiness.QuicIdentity.Name,
                    readiness.QuicIdentity.Address,
                    readiness.QuicIdentity.Port,
                    readiness.QuicIdentity.TlsEnabled));
    }
}
