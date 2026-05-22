namespace MDRAVA.BLL.ControlPlane;

public sealed record ProxyListenerStatus(
    string Name,
    string Identity,
    string BindKey,
    string Kind,
    string Address,
    int Port,
    string Transport,
    bool TlsEnabled,
    string Protocols,
    ProxyListenerHttp3Status Http3,
    int Http2MaxConcurrentStreams,
    int Http2MaxHeaderListBytes,
    int Http2MaxFrameSize,
    ProxyListenerState State,
    long ActiveConnections,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? StoppedAtUtc,
    string? LastError);

public sealed record ProxyListenerHttp3Status(
    bool Configured,
    bool DefaultEnabled,
    string EnablementLevel,
    bool EnabledForTraffic,
    string DisabledReason,
    bool AltSvcConfigured,
    int AltSvcMaxAgeSeconds,
    bool UdpQuicListenerIdentityModeled,
    ProxyQuicListenerIdentity? QuicIdentity);

public sealed record ProxyQuicListenerIdentity(
    string Name,
    string Address,
    int Port,
    bool TlsEnabled)
{
    public string Key => $"{Normalize(Name)}|quic";

    public string BindKey => $"{Normalize(Address)}|{Port}|udp|quic";

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }
}
