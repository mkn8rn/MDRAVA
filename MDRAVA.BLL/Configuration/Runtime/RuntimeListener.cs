namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeListener
{
    public RuntimeListener(
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
    }

    public string Name { get; init; }

    public string Address { get; init; }

    public int Port { get; init; }

    public bool Enabled { get; init; }

    public RuntimeListenerTransport Transport { get; init; }

    public string? DefaultCertificateId { get; init; }

    public IReadOnlyList<RuntimeSniCertificateBinding> SniCertificates { get; }

    public int Backlog { get; init; }

    public int MaxRequestHeadBytes { get; init; }

    public int MaxResponseHeadBytes { get; init; }

    public int MaxChunkLineBytes { get; init; }

    public int ForwardingBufferBytes { get; init; }

    public RuntimeListenerIdentity Identity => RuntimeListenerIdentity.From(this);

    public RuntimeListenerProtocols Protocols { get; init; } = RuntimeListenerProtocols.Http1;

    public RuntimeHttp3Enablement Http3Enablement { get; init; } = RuntimeHttp3Enablement.Default;

    public RuntimeHttp3AltSvcOptions Http3AltSvc { get; init; } = RuntimeHttp3AltSvcOptions.Disabled;

    public RuntimeHttp2Limits Http2Limits { get; init; } = RuntimeHttp2Limits.Default;

    public bool TcpTrafficEnabled => Protocols.HasTcpProtocols();

    public bool Http3ProtocolConfigured => Protocols.HasHttp3();

    public RuntimeQuicListenerIdentity? QuicIdentity => Http3.EnabledForTraffic
        ? RuntimeQuicListenerIdentity.From(this)
        : null;

    public RuntimeHttp3ListenerReadiness Http3 => RuntimeHttp3ListenerReadiness.From(this);

    public RuntimeListener WithSniCertificates(
        IReadOnlyList<RuntimeSniCertificateBinding> sniCertificates)
    {
        return new RuntimeListener(
            Name,
            Address,
            Port,
            Enabled,
            Transport,
            DefaultCertificateId,
            sniCertificates,
            Backlog,
            MaxRequestHeadBytes,
            MaxResponseHeadBytes,
            MaxChunkLineBytes,
            ForwardingBufferBytes)
        {
            Protocols = Protocols,
            Http3Enablement = Http3Enablement,
            Http3AltSvc = Http3AltSvc,
            Http2Limits = Http2Limits
        };
    }
}
