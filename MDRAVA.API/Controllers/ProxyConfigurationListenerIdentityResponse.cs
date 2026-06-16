using BusinessRuntimeListenerIdentityProjection = MDRAVA.BLL.Configuration.RuntimeListenerIdentityProjection;
using BusinessRuntimeListenerTransport = MDRAVA.BLL.Configuration.RuntimeListenerTransport;
using BusinessRuntimeSniCertificateBindingProjection = MDRAVA.BLL.Configuration.RuntimeSniCertificateBindingProjection;

namespace MDRAVA.API.Controllers;

public sealed record RuntimeListenerIdentityResponse
{
    public RuntimeListenerIdentityResponse(
        string name,
        string address,
        int port,
        RuntimeListenerTransportResponse transport,
        bool tlsEnabled,
        string key,
        string bindKey)
    {
        Name = name;
        Address = address;
        Port = port;
        Transport = transport;
        TlsEnabled = tlsEnabled;
        Key = key;
        BindKey = bindKey;
    }

    public string Name { get; }

    public string Address { get; }

    public int Port { get; }

    public RuntimeListenerTransportResponse Transport { get; }

    public bool TlsEnabled { get; }

    public string Key { get; }

    public string BindKey { get; }

    public static RuntimeListenerIdentityResponse FromProjection(BusinessRuntimeListenerIdentityProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeListenerIdentityResponse(
            name: projection.Name,
            address: projection.Address,
            port: projection.Port,
            transport: RuntimeListenerTransportResponseMapper.FromTransport(projection.Transport),
            tlsEnabled: projection.TlsEnabled,
            key: projection.Key,
            bindKey: projection.BindKey);
    }
}

public sealed record RuntimeSniCertificateBindingResponse(
    string HostName,
    string CertificateId)
{
    public static IReadOnlyList<RuntimeSniCertificateBindingResponse> FromBindings(
        IReadOnlyList<BusinessRuntimeSniCertificateBindingProjection> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        return ApiResponseList.Copy(bindings.Select(FromBinding));
    }

    private static RuntimeSniCertificateBindingResponse FromBinding(
        BusinessRuntimeSniCertificateBindingProjection binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        return new RuntimeSniCertificateBindingResponse(binding.HostName, binding.CertificateId);
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
