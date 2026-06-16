using BusinessRuntimeHttp2LimitsProjection = MDRAVA.BLL.Configuration.RuntimeHttp2LimitsProjection;
using BusinessRuntimeListenerProjection = MDRAVA.BLL.Configuration.RuntimeListenerProjection;
using BusinessRuntimeListenerProtocols = MDRAVA.BLL.Configuration.RuntimeListenerProtocols;

namespace MDRAVA.API.Controllers;

public sealed record RuntimeListenerResponse
{
    public RuntimeListenerResponse(
        string name,
        string address,
        int port,
        bool enabled,
        RuntimeListenerTransportResponse transport,
        string? defaultCertificateId,
        IReadOnlyList<RuntimeSniCertificateBindingResponse> sniCertificates,
        int backlog,
        int maxRequestHeadBytes,
        int maxResponseHeadBytes,
        int maxChunkLineBytes,
        int forwardingBufferBytes,
        RuntimeListenerIdentityResponse identity,
        RuntimeListenerProtocolsResponse protocols,
        RuntimeHttp3EnablementResponse http3Enablement,
        RuntimeHttp3AltSvcResponse http3AltSvc,
        RuntimeHttp2LimitsResponse http2Limits,
        bool tcpTrafficEnabled,
        bool http3ProtocolConfigured,
        RuntimeQuicListenerIdentityResponse? quicIdentity,
        RuntimeHttp3ListenerReadinessResponse http3)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(http3AltSvc);
        ArgumentNullException.ThrowIfNull(http2Limits);
        ArgumentNullException.ThrowIfNull(http3);

        Name = name;
        Address = address;
        Port = port;
        Enabled = enabled;
        Transport = transport;
        DefaultCertificateId = defaultCertificateId;
        SniCertificates = ApiResponseList.Copy(sniCertificates);
        Backlog = backlog;
        MaxRequestHeadBytes = maxRequestHeadBytes;
        MaxResponseHeadBytes = maxResponseHeadBytes;
        MaxChunkLineBytes = maxChunkLineBytes;
        ForwardingBufferBytes = forwardingBufferBytes;
        Identity = identity;
        Protocols = protocols;
        Http3Enablement = http3Enablement;
        Http3AltSvc = http3AltSvc;
        Http2Limits = http2Limits;
        TcpTrafficEnabled = tcpTrafficEnabled;
        Http3ProtocolConfigured = http3ProtocolConfigured;
        QuicIdentity = quicIdentity;
        Http3 = http3;
    }

    public string Name { get; }

    public string Address { get; }

    public int Port { get; }

    public bool Enabled { get; }

    public RuntimeListenerTransportResponse Transport { get; }

    public string? DefaultCertificateId { get; }

    public IReadOnlyList<RuntimeSniCertificateBindingResponse> SniCertificates { get; }

    public int Backlog { get; }

    public int MaxRequestHeadBytes { get; }

    public int MaxResponseHeadBytes { get; }

    public int MaxChunkLineBytes { get; }

    public int ForwardingBufferBytes { get; }

    public RuntimeListenerIdentityResponse Identity { get; }

    public RuntimeListenerProtocolsResponse Protocols { get; }

    public RuntimeHttp3EnablementResponse Http3Enablement { get; }

    public RuntimeHttp3AltSvcResponse Http3AltSvc { get; }

    public RuntimeHttp2LimitsResponse Http2Limits { get; }

    public bool TcpTrafficEnabled { get; }

    public bool Http3ProtocolConfigured { get; }

    public RuntimeQuicListenerIdentityResponse? QuicIdentity { get; }

    public RuntimeHttp3ListenerReadinessResponse Http3 { get; }

    public static IReadOnlyList<RuntimeListenerResponse> FromListeners(
        IReadOnlyList<BusinessRuntimeListenerProjection> listeners)
    {
        ArgumentNullException.ThrowIfNull(listeners);

        return ApiResponseList.Copy(listeners.Select(FromListener));
    }

    private static RuntimeListenerResponse FromListener(BusinessRuntimeListenerProjection listener)
    {
        ArgumentNullException.ThrowIfNull(listener);

        return new RuntimeListenerResponse(
            name: listener.Name,
            address: listener.Address,
            port: listener.Port,
            enabled: listener.Enabled,
            transport: RuntimeListenerTransportResponseMapper.FromTransport(listener.Transport),
            defaultCertificateId: listener.DefaultCertificateId,
            sniCertificates: RuntimeSniCertificateBindingResponse.FromBindings(listener.SniCertificates),
            backlog: listener.Backlog,
            maxRequestHeadBytes: listener.MaxRequestHeadBytes,
            maxResponseHeadBytes: listener.MaxResponseHeadBytes,
            maxChunkLineBytes: listener.MaxChunkLineBytes,
            forwardingBufferBytes: listener.ForwardingBufferBytes,
            identity: RuntimeListenerIdentityResponse.FromProjection(listener.Identity),
            protocols: RuntimeListenerProtocolsResponseMapper.FromProtocols(listener.Protocols),
            http3Enablement: RuntimeHttp3EnablementResponseMapper.FromEnablement(listener.Http3Enablement),
            http3AltSvc: RuntimeHttp3AltSvcResponse.FromProjection(listener.Http3AltSvc),
            http2Limits: RuntimeHttp2LimitsResponse.FromProjection(listener.Http2Limits),
            tcpTrafficEnabled: listener.TcpTrafficEnabled,
            http3ProtocolConfigured: listener.Http3ProtocolConfigured,
            quicIdentity: listener.QuicIdentity is null
                ? null
                : RuntimeQuicListenerIdentityResponse.FromProjection(listener.QuicIdentity),
            http3: RuntimeHttp3ListenerReadinessResponse.FromProjection(listener.Http3));
    }
}

public sealed record RuntimeHttp2LimitsResponse(
    int MaxConcurrentStreams,
    int MaxHeaderListBytes,
    int MaxFrameSize)
{
    public static RuntimeHttp2LimitsResponse FromProjection(BusinessRuntimeHttp2LimitsProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeHttp2LimitsResponse(
            projection.MaxConcurrentStreams,
            projection.MaxHeaderListBytes,
            projection.MaxFrameSize);
    }
}

[Flags]
public enum RuntimeListenerProtocolsResponse
{
    None = 0,
    Http1 = 1,
    Http2 = 2,
    Http3 = 4,
    Http1AndHttp2 = Http1 | Http2,
    Http1AndHttp3 = Http1 | Http3,
    Http2AndHttp3 = Http2 | Http3,
    Http1AndHttp2AndHttp3 = Http1 | Http2 | Http3
}

public static class RuntimeListenerProtocolsResponseMapper
{
    public static RuntimeListenerProtocolsResponse FromProtocols(BusinessRuntimeListenerProtocols protocols)
    {
        return protocols switch
        {
            BusinessRuntimeListenerProtocols.None => RuntimeListenerProtocolsResponse.None,
            BusinessRuntimeListenerProtocols.Http1 => RuntimeListenerProtocolsResponse.Http1,
            BusinessRuntimeListenerProtocols.Http2 => RuntimeListenerProtocolsResponse.Http2,
            BusinessRuntimeListenerProtocols.Http3 => RuntimeListenerProtocolsResponse.Http3,
            BusinessRuntimeListenerProtocols.Http1AndHttp2 => RuntimeListenerProtocolsResponse.Http1AndHttp2,
            BusinessRuntimeListenerProtocols.Http1AndHttp3 => RuntimeListenerProtocolsResponse.Http1AndHttp3,
            BusinessRuntimeListenerProtocols.Http2AndHttp3 => RuntimeListenerProtocolsResponse.Http2AndHttp3,
            BusinessRuntimeListenerProtocols.Http1AndHttp2AndHttp3 =>
                RuntimeListenerProtocolsResponse.Http1AndHttp2AndHttp3,
            _ => throw new ArgumentOutOfRangeException(nameof(protocols), protocols, null)
        };
    }
}
