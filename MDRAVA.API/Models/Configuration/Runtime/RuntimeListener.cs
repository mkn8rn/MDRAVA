namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeListener(
    string Name,
    string Address,
    int Port,
    bool Enabled,
    RuntimeListenerTransport Transport,
    string? DefaultCertificateId,
    IReadOnlyList<RuntimeSniCertificateBinding> SniCertificates,
    int Backlog,
    int MaxRequestHeadBytes,
    int MaxResponseHeadBytes,
    int MaxChunkLineBytes,
    int ForwardingBufferBytes)
{
    public RuntimeListenerIdentity Identity => RuntimeListenerIdentity.From(this);

    public RuntimeListenerProtocols Protocols { get; init; } = RuntimeListenerProtocols.Http1;

    public RuntimeHttp3Enablement Http3Enablement { get; init; } = RuntimeHttp3Enablement.Default;

    public RuntimeHttp3AltSvcOptions Http3AltSvc { get; init; } = RuntimeHttp3AltSvcOptions.Disabled;

    public int Http3MaxBufferedRequestBodyBytes { get; init; }

    public RuntimeHttp2Limits Http2Limits { get; init; } = RuntimeHttp2Limits.Default;

    public bool TcpTrafficEnabled => Protocols.HasTcpProtocols();

    public bool Http3ProtocolConfigured => Protocols.HasHttp3();

    public RuntimeQuicListenerIdentity? QuicIdentity => Http3.EnabledForTraffic
        ? RuntimeQuicListenerIdentity.From(this)
        : null;

    public RuntimeHttp3ListenerReadiness Http3 => RuntimeHttp3ListenerReadiness.From(this);
}
