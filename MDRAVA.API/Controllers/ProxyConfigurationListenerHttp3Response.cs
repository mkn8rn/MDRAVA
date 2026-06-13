using BusinessRuntimeHttp3AltSvcOptions = MDRAVA.BLL.Configuration.RuntimeHttp3AltSvcOptions;
using BusinessRuntimeHttp3Enablement = MDRAVA.BLL.Configuration.RuntimeHttp3Enablement;
using BusinessRuntimeHttp3ListenerReadiness = MDRAVA.BLL.Configuration.RuntimeHttp3ListenerReadiness;
using BusinessRuntimeQuicListenerIdentity = MDRAVA.BLL.Configuration.RuntimeQuicListenerIdentity;

namespace MDRAVA.API.Controllers;

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
