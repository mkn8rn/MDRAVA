using BusinessProxyListenerHttp3Status = MDRAVA.BLL.ControlPlane.Listeners.ProxyListenerHttp3Status;
using BusinessProxyListenerState = MDRAVA.BLL.ControlPlane.Listeners.ProxyListenerState;
using BusinessProxyListenerStatus = MDRAVA.BLL.ControlPlane.Listeners.ProxyListenerStatus;
using BusinessProxyQuicListenerIdentity = MDRAVA.BLL.ControlPlane.Listeners.ProxyQuicListenerIdentity;

namespace MDRAVA.API.Controllers;

public sealed record ProxyListenerStatusResponse(
    string Name,
    string Identity,
    string BindKey,
    string Kind,
    string Address,
    int Port,
    string Transport,
    bool TlsEnabled,
    string Protocols,
    ProxyListenerHttp3StatusResponse Http3,
    int Http2MaxConcurrentStreams,
    int Http2MaxHeaderListBytes,
    int Http2MaxFrameSize,
    ProxyListenerStateResponse State,
    long ActiveConnections,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? StoppedAtUtc,
    string? LastError)
{
    public static IReadOnlyList<ProxyListenerStatusResponse> FromStatuses(
        IReadOnlyList<BusinessProxyListenerStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(statuses);

        return statuses.Select(FromStatus).ToArray();
    }

    private static ProxyListenerStatusResponse FromStatus(BusinessProxyListenerStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return new ProxyListenerStatusResponse(
            status.Name,
            status.Identity,
            status.BindKey,
            status.Kind,
            status.Address,
            status.Port,
            status.Transport,
            status.TlsEnabled,
            status.Protocols,
            ProxyListenerHttp3StatusResponse.FromStatus(status.Http3),
            status.Http2MaxConcurrentStreams,
            status.Http2MaxHeaderListBytes,
            status.Http2MaxFrameSize,
            ProxyListenerStateResponseMapper.FromState(status.State),
            status.ActiveConnections,
            status.StartedAtUtc,
            status.StoppedAtUtc,
            status.LastError);
    }
}

public sealed record ProxyListenerHttp3StatusResponse(
    bool Configured,
    bool DefaultEnabled,
    string EnablementLevel,
    bool EnabledForTraffic,
    string DisabledReason,
    bool AltSvcConfigured,
    int AltSvcMaxAgeSeconds,
    bool UdpQuicListenerIdentityModeled,
    ProxyQuicListenerIdentityResponse? QuicIdentity)
{
    public static ProxyListenerHttp3StatusResponse FromStatus(BusinessProxyListenerHttp3Status status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return new ProxyListenerHttp3StatusResponse(
            status.Configured,
            status.DefaultEnabled,
            status.EnablementLevel,
            status.EnabledForTraffic,
            status.DisabledReason,
            status.AltSvcConfigured,
            status.AltSvcMaxAgeSeconds,
            status.UdpQuicListenerIdentityModeled,
            status.QuicIdentity is null
                ? null
                : ProxyQuicListenerIdentityResponse.FromIdentity(status.QuicIdentity));
    }
}

public sealed record ProxyQuicListenerIdentityResponse(
    string Name,
    string Address,
    int Port,
    bool TlsEnabled,
    string Key,
    string BindKey)
{
    public static ProxyQuicListenerIdentityResponse FromIdentity(BusinessProxyQuicListenerIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        return new ProxyQuicListenerIdentityResponse(
            identity.Name,
            identity.Address,
            identity.Port,
            identity.TlsEnabled,
            identity.Key,
            identity.BindKey);
    }
}

public enum ProxyListenerStateResponse
{
    Starting = 0,
    Active = 1,
    Draining = 2,
    Stopped = 3,
    Failed = 4
}

public static class ProxyListenerStateResponseMapper
{
    public static ProxyListenerStateResponse FromState(BusinessProxyListenerState state)
    {
        return state switch
        {
            BusinessProxyListenerState.Starting => ProxyListenerStateResponse.Starting,
            BusinessProxyListenerState.Active => ProxyListenerStateResponse.Active,
            BusinessProxyListenerState.Draining => ProxyListenerStateResponse.Draining,
            BusinessProxyListenerState.Stopped => ProxyListenerStateResponse.Stopped,
            BusinessProxyListenerState.Failed => ProxyListenerStateResponse.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };
    }
}
