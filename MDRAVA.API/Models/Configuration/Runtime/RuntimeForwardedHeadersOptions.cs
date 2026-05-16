namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeForwardedHeadersOptions(
    bool Enabled,
    IReadOnlyList<RuntimeTrustedProxy> TrustedProxies);
