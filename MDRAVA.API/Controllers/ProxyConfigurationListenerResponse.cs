using BusinessRuntimeHttp2Limits = MDRAVA.BLL.Configuration.RuntimeHttp2Limits;
using BusinessRuntimeHttp3AltSvcOptions = MDRAVA.BLL.Configuration.RuntimeHttp3AltSvcOptions;
using BusinessRuntimeHttp3Enablement = MDRAVA.BLL.Configuration.RuntimeHttp3Enablement;
using BusinessRuntimeHttp3ListenerReadiness = MDRAVA.BLL.Configuration.RuntimeHttp3ListenerReadiness;
using BusinessRuntimeListener = MDRAVA.BLL.Configuration.RuntimeListener;
using BusinessRuntimeListenerIdentity = MDRAVA.BLL.Configuration.RuntimeListenerIdentity;
using BusinessRuntimeListenerProtocols = MDRAVA.BLL.Configuration.RuntimeListenerProtocols;
using BusinessRuntimeListenerTransport = MDRAVA.BLL.Configuration.RuntimeListenerTransport;
using BusinessRuntimeQuicListenerIdentity = MDRAVA.BLL.Configuration.RuntimeQuicListenerIdentity;
using BusinessRuntimeSniCertificateBinding = MDRAVA.BLL.Configuration.RuntimeSniCertificateBinding;

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
        IReadOnlyList<BusinessRuntimeListener> listeners)
    {
        ArgumentNullException.ThrowIfNull(listeners);

        return listeners.Select(FromListener).ToArray();
    }

    private static RuntimeListenerResponse FromListener(BusinessRuntimeListener listener)
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
            Identity = RuntimeListenerIdentityResponse.FromIdentity(listener.Identity),
            Protocols = RuntimeListenerProtocolsResponseMapper.FromProtocols(listener.Protocols),
            Http3Enablement = RuntimeHttp3EnablementResponseMapper.FromEnablement(listener.Http3Enablement),
            Http3AltSvc = RuntimeHttp3AltSvcResponse.FromOptions(listener.Http3AltSvc),
            Http2Limits = RuntimeHttp2LimitsResponse.FromLimits(listener.Http2Limits),
            TcpTrafficEnabled = listener.TcpTrafficEnabled,
            Http3ProtocolConfigured = listener.Http3ProtocolConfigured,
            QuicIdentity = listener.QuicIdentity is null
                ? null
                : RuntimeQuicListenerIdentityResponse.FromIdentity(listener.QuicIdentity),
            Http3 = RuntimeHttp3ListenerReadinessResponse.FromReadiness(listener.Http3)
        };
    }
}

public sealed record RuntimeListenerIdentityResponse(
    string Name,
    string Address,
    int Port,
    RuntimeListenerTransportResponse Transport,
    bool TlsEnabled)
{
    public string Key { get; init; } = "";

    public string BindKey { get; init; } = "";

    public static RuntimeListenerIdentityResponse FromIdentity(BusinessRuntimeListenerIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        return new RuntimeListenerIdentityResponse(
            identity.Name,
            identity.Address,
            identity.Port,
            RuntimeListenerTransportResponseMapper.FromTransport(identity.Transport),
            identity.TlsEnabled)
        {
            Key = identity.Key,
            BindKey = identity.BindKey
        };
    }
}

public sealed record RuntimeSniCertificateBindingResponse(
    string HostName,
    string CertificateId)
{
    public static IReadOnlyList<RuntimeSniCertificateBindingResponse> FromBindings(
        IReadOnlyList<BusinessRuntimeSniCertificateBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        return bindings.Select(FromBinding).ToArray();
    }

    private static RuntimeSniCertificateBindingResponse FromBinding(BusinessRuntimeSniCertificateBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        return new RuntimeSniCertificateBindingResponse(binding.HostName, binding.CertificateId);
    }
}

public sealed record RuntimeHttp3AltSvcResponse(
    bool Enabled,
    int MaxAgeSeconds)
{
    public static RuntimeHttp3AltSvcResponse FromOptions(BusinessRuntimeHttp3AltSvcOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new RuntimeHttp3AltSvcResponse(options.Enabled, options.MaxAgeSeconds);
    }
}

public sealed record RuntimeHttp2LimitsResponse(
    int MaxConcurrentStreams,
    int MaxHeaderListBytes,
    int MaxFrameSize)
{
    public static RuntimeHttp2LimitsResponse FromLimits(BusinessRuntimeHttp2Limits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);

        return new RuntimeHttp2LimitsResponse(
            limits.MaxConcurrentStreams,
            limits.MaxHeaderListBytes,
            limits.MaxFrameSize);
    }
}

public sealed record RuntimeHttp3ListenerReadinessResponse(
    bool Configured,
    bool DefaultEnabled,
    string EnablementLevel,
    bool EnabledForTraffic,
    string DisabledReason,
    bool AltSvcConfigured,
    int AltSvcMaxAgeSeconds,
    bool UdpQuicListenerIdentityModeled,
    RuntimeQuicListenerIdentityResponse? QuicIdentity)
{
    public static RuntimeHttp3ListenerReadinessResponse FromReadiness(
        BusinessRuntimeHttp3ListenerReadiness readiness)
    {
        ArgumentNullException.ThrowIfNull(readiness);

        return new RuntimeHttp3ListenerReadinessResponse(
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
                : RuntimeQuicListenerIdentityResponse.FromIdentity(readiness.QuicIdentity));
    }
}

public sealed record RuntimeQuicListenerIdentityResponse(
    string Name,
    string Address,
    int Port,
    bool TlsEnabled)
{
    public string Key { get; init; } = "";

    public string BindKey { get; init; } = "";

    public static RuntimeQuicListenerIdentityResponse FromIdentity(BusinessRuntimeQuicListenerIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        return new RuntimeQuicListenerIdentityResponse(
            identity.Name,
            identity.Address,
            identity.Port,
            identity.TlsEnabled)
        {
            Key = identity.Key,
            BindKey = identity.BindKey
        };
    }
}

public enum RuntimeListenerTransportResponse
{
    Http = 0,
    Https = 1
}

public static class RuntimeListenerTransportResponseMapper
{
    public static RuntimeListenerTransportResponse FromTransport(BusinessRuntimeListenerTransport transport)
    {
        return transport switch
        {
            BusinessRuntimeListenerTransport.Http => RuntimeListenerTransportResponse.Http,
            BusinessRuntimeListenerTransport.Https => RuntimeListenerTransportResponse.Https,
            _ => throw new ArgumentOutOfRangeException(nameof(transport), transport, null)
        };
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

public enum RuntimeHttp3EnablementResponse
{
    Default = 0,
    Disabled = 1
}

public static class RuntimeHttp3EnablementResponseMapper
{
    public static RuntimeHttp3EnablementResponse FromEnablement(BusinessRuntimeHttp3Enablement enablement)
    {
        return enablement switch
        {
            BusinessRuntimeHttp3Enablement.Default => RuntimeHttp3EnablementResponse.Default,
            BusinessRuntimeHttp3Enablement.Disabled => RuntimeHttp3EnablementResponse.Disabled,
            _ => throw new ArgumentOutOfRangeException(nameof(enablement), enablement, null)
        };
    }
}
