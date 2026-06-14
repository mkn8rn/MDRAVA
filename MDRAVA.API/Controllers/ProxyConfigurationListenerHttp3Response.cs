using BusinessRuntimeHttp3AltSvcProjection = MDRAVA.BLL.Configuration.RuntimeHttp3AltSvcProjection;
using BusinessRuntimeHttp3Enablement = MDRAVA.BLL.Configuration.RuntimeHttp3Enablement;
using BusinessRuntimeHttp3ListenerReadinessProjection =
    MDRAVA.BLL.Configuration.RuntimeHttp3ListenerReadinessProjection;
using BusinessRuntimeQuicListenerIdentityProjection = MDRAVA.BLL.Configuration.RuntimeQuicListenerIdentityProjection;

namespace MDRAVA.API.Controllers;

public sealed record RuntimeHttp3AltSvcResponse(
    bool Enabled,
    int MaxAgeSeconds)
{
    public static RuntimeHttp3AltSvcResponse FromProjection(BusinessRuntimeHttp3AltSvcProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeHttp3AltSvcResponse(projection.Enabled, projection.MaxAgeSeconds);
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
    public static RuntimeHttp3ListenerReadinessResponse FromProjection(
        BusinessRuntimeHttp3ListenerReadinessProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeHttp3ListenerReadinessResponse(
            projection.Configured,
            projection.DefaultEnabled,
            projection.EnablementLevel,
            projection.EnabledForTraffic,
            projection.DisabledReason,
            projection.AltSvcConfigured,
            projection.AltSvcMaxAgeSeconds,
            projection.UdpQuicListenerIdentityModeled,
            projection.QuicIdentity is null
                ? null
                : RuntimeQuicListenerIdentityResponse.FromProjection(projection.QuicIdentity));
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

    public static RuntimeQuicListenerIdentityResponse FromProjection(
        BusinessRuntimeQuicListenerIdentityProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeQuicListenerIdentityResponse(
            projection.Name,
            projection.Address,
            projection.Port,
            projection.TlsEnabled)
        {
            Key = projection.Key,
            BindKey = projection.BindKey
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
