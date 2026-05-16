namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeCanonicalHostPolicy(
    bool Enabled,
    string TargetHost,
    int StatusCode);
