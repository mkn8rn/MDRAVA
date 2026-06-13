using BusinessProxyListenerStatus = MDRAVA.BLL.ControlPlane.Listeners.ProxyListenerStatus;

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
