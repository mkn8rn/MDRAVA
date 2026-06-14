namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeHttp3AltSvcProjection(
    bool Enabled,
    int MaxAgeSeconds);
