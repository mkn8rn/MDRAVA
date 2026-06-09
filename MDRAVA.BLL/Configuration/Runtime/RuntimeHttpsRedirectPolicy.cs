namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeHttpsRedirectPolicy(
    bool Enabled,
    int StatusCode,
    int? HttpsPort);
