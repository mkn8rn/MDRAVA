namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeHttpsRedirectProjection(
    bool Enabled,
    int StatusCode,
    int? HttpsPort);
