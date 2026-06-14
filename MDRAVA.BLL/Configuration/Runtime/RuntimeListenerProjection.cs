namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeListenerProjection
{
    public RuntimeListenerProjection(
        string Name,
        string Address,
        int Port,
        bool Enabled,
        RuntimeListenerTransport Transport,
        string? DefaultCertificateId,
        IReadOnlyList<RuntimeSniCertificateBindingProjection> SniCertificates,
        int Backlog,
        int MaxRequestHeadBytes,
        int MaxResponseHeadBytes,
        int MaxChunkLineBytes,
        int ForwardingBufferBytes,
        RuntimeListenerIdentityProjection Identity,
        RuntimeListenerProtocols Protocols,
        RuntimeHttp3Enablement Http3Enablement,
        RuntimeHttp3AltSvcProjection Http3AltSvc,
        RuntimeHttp2LimitsProjection Http2Limits,
        bool TcpTrafficEnabled,
        bool Http3ProtocolConfigured,
        RuntimeQuicListenerIdentity? QuicIdentity,
        RuntimeHttp3ListenerReadiness Http3)
    {
        this.Name = Name;
        this.Address = Address;
        this.Port = Port;
        this.Enabled = Enabled;
        this.Transport = Transport;
        this.DefaultCertificateId = DefaultCertificateId;
        this.SniCertificates = RuntimeList.Copy(SniCertificates);
        this.Backlog = Backlog;
        this.MaxRequestHeadBytes = MaxRequestHeadBytes;
        this.MaxResponseHeadBytes = MaxResponseHeadBytes;
        this.MaxChunkLineBytes = MaxChunkLineBytes;
        this.ForwardingBufferBytes = ForwardingBufferBytes;
        this.Identity = Identity;
        this.Protocols = Protocols;
        this.Http3Enablement = Http3Enablement;
        this.Http3AltSvc = Http3AltSvc;
        this.Http2Limits = Http2Limits;
        this.TcpTrafficEnabled = TcpTrafficEnabled;
        this.Http3ProtocolConfigured = Http3ProtocolConfigured;
        this.QuicIdentity = QuicIdentity;
        this.Http3 = Http3;
    }

    public string Name { get; init; }

    public string Address { get; init; }

    public int Port { get; init; }

    public bool Enabled { get; init; }

    public RuntimeListenerTransport Transport { get; init; }

    public string? DefaultCertificateId { get; init; }

    public IReadOnlyList<RuntimeSniCertificateBindingProjection> SniCertificates { get; }

    public int Backlog { get; init; }

    public int MaxRequestHeadBytes { get; init; }

    public int MaxResponseHeadBytes { get; init; }

    public int MaxChunkLineBytes { get; init; }

    public int ForwardingBufferBytes { get; init; }

    public RuntimeListenerIdentityProjection Identity { get; init; }

    public RuntimeListenerProtocols Protocols { get; init; }

    public RuntimeHttp3Enablement Http3Enablement { get; init; }

    public RuntimeHttp3AltSvcProjection Http3AltSvc { get; init; }

    public RuntimeHttp2LimitsProjection Http2Limits { get; init; }

    public bool TcpTrafficEnabled { get; init; }

    public bool Http3ProtocolConfigured { get; init; }

    public RuntimeQuicListenerIdentity? QuicIdentity { get; init; }

    public RuntimeHttp3ListenerReadiness Http3 { get; init; }
}
