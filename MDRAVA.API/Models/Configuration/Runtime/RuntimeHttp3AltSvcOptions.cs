namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeHttp3AltSvcOptions(
    bool Enabled,
    int MaxAgeSeconds)
{
    public static RuntimeHttp3AltSvcOptions Disabled { get; } = new(false, 86400);
}
