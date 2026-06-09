namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeHttp3AltSvcOptions(
    bool Enabled,
    int MaxAgeSeconds)
{
    public static RuntimeHttp3AltSvcOptions Disabled { get; } = new(false, 86400);
}
