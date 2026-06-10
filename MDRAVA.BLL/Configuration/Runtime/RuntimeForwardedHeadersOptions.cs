namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeForwardedHeadersOptions(
    bool Enabled,
    IReadOnlyList<string> TrustedProxies);
