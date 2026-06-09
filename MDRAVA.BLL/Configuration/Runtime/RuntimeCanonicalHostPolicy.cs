namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeCanonicalHostPolicy(
    bool Enabled,
    string TargetHost,
    int StatusCode);
