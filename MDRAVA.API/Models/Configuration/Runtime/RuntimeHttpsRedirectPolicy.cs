namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeHttpsRedirectPolicy(
    bool Enabled,
    int StatusCode,
    int? HttpsPort);
