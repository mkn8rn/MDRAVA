namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeCanonicalHostProjection(
    bool Enabled,
    string TargetHost,
    int StatusCode);
