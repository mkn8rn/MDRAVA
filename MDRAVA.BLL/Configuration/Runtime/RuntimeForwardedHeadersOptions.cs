namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeForwardedHeadersOptions(
    bool Enabled,
    IReadOnlyList<RuntimeTrustedProxy> TrustedProxies);
