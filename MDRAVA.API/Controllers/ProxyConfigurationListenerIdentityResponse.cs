using BusinessRuntimeListenerIdentity = MDRAVA.BLL.Configuration.RuntimeListenerIdentity;
using BusinessRuntimeListenerTransport = MDRAVA.BLL.Configuration.RuntimeListenerTransport;
using BusinessRuntimeSniCertificateBinding = MDRAVA.BLL.Configuration.RuntimeSniCertificateBinding;

namespace MDRAVA.API.Controllers;

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
