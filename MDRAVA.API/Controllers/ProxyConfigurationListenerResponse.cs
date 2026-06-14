using BusinessRuntimeHttp2LimitsProjection = MDRAVA.BLL.Configuration.RuntimeHttp2LimitsProjection;
using BusinessRuntimeListenerProjection = MDRAVA.BLL.Configuration.RuntimeListenerProjection;
using BusinessRuntimeListenerProtocols = MDRAVA.BLL.Configuration.RuntimeListenerProtocols;

namespace MDRAVA.API.Controllers;

public sealed record RuntimeListenerResponse(
    string Name,
    string Address,
    int Port,
    bool Enabled,
    RuntimeListenerTransportResponse Transport,
    string? DefaultCertificateId,
    IReadOnlyList<RuntimeSniCertificateBindingResponse> SniCertificates,
    int Backlog,
    int MaxRequestHeadBytes,
    int MaxResponseHeadBytes,
    int MaxChunkLineBytes,
    int ForwardingBufferBytes)
{
    public RuntimeListenerIdentityResponse Identity { get; init; } = null!;

    public RuntimeListenerProtocolsResponse Protocols { get; init; }

    public RuntimeHttp3EnablementResponse Http3Enablement { get; init; }

    public RuntimeHttp3AltSvcResponse Http3AltSvc { get; init; } = null!;

    public RuntimeHttp2LimitsResponse Http2Limits { get; init; } = null!;

    public bool TcpTrafficEnabled { get; init; }

    public bool Http3ProtocolConfigured { get; init; }

    public RuntimeQuicListenerIdentityResponse? QuicIdentity { get; init; }

    public RuntimeHttp3ListenerReadinessResponse Http3 { get; init; } = null!;

    public static IReadOnlyList<RuntimeListenerResponse> FromListeners(
        IReadOnlyList<BusinessRuntimeListenerProjection> listeners)
    {
        ArgumentNullException.ThrowIfNull(listeners);

        return listeners.Select(FromListener).ToArray();
    }

    private static RuntimeListenerResponse FromListener(BusinessRuntimeListenerProjection listener)
    {
        ArgumentNullException.ThrowIfNull(listener);

        return new RuntimeListenerResponse(
            listener.Name,
            listener.Address,
            listener.Port,
            listener.Enabled,
            RuntimeListenerTransportResponseMapper.FromTransport(listener.Transport),
            listener.DefaultCertificateId,
            RuntimeSniCertificateBindingResponse.FromBindings(listener.SniCertificates),
            listener.Backlog,
            listener.MaxRequestHeadBytes,
            listener.MaxResponseHeadBytes,
            listener.MaxChunkLineBytes,
            listener.ForwardingBufferBytes)
        {
            Identity = RuntimeListenerIdentityResponse.FromProjection(listener.Identity),
            Protocols = RuntimeListenerProtocolsResponseMapper.FromProtocols(listener.Protocols),
            Http3Enablement = RuntimeHttp3EnablementResponseMapper.FromEnablement(listener.Http3Enablement),
            Http3AltSvc = RuntimeHttp3AltSvcResponse.FromProjection(listener.Http3AltSvc),
            Http2Limits = RuntimeHttp2LimitsResponse.FromProjection(listener.Http2Limits),
            TcpTrafficEnabled = listener.TcpTrafficEnabled,
            Http3ProtocolConfigured = listener.Http3ProtocolConfigured,
            QuicIdentity = listener.QuicIdentity is null
                ? null
                : RuntimeQuicListenerIdentityResponse.FromProjection(listener.QuicIdentity),
            Http3 = RuntimeHttp3ListenerReadinessResponse.FromProjection(listener.Http3)
        };
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
